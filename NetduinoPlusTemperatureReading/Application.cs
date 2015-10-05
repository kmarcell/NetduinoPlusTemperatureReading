using System;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;

using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

using CloudLib;
using Logger;

namespace NetduinoPlusTemperatureReading
{
    class Application : IApplication
    {
        private XbeeDevice xbeeCoordinator;
        private ICloudPlatform upstreamMQTT;
        private OutputPort onboardLED = new OutputPort(Pins.ONBOARD_LED, false);

        public void applicationWillStart()
        {
            // Logging
            NDLogger.SetLogLevel(LogLevel.Verbose);
            NDLogger.Log("Program started!");

            NDLogger.Log("Waiting for DHCP to set up.", LogLevel.Verbose);
            onboardLED.Write(true);
        }

        public void didFinishLaunching()
        {
            // Ehernet didSetup
            onboardLED.Write(false);
            NetworkInterface NI = NetworkInterface.GetAllNetworkInterfaces()[0];
            NDLogger.Log("Ethernet IP " + NI.IPAddress.ToString(), LogLevel.Verbose);

            xbeeCoordinator = new XbeeDevice(createSerialPortWithName("COM1"));

            xbeeCoordinator.BytesReadFromSerial += new BytesReadFromSerialEventHandler(BytesReadFromSerialHandler);
            xbeeCoordinator.FrameDroppedByChecksum += new FrameDroppedByChecksumEventHandler(FrameDroppedByChecksumHandler);
            xbeeCoordinator.ReceivedRemoteFrame += new ReceivedRemoteFrameEventHandler(ReceivedRemoteFrameHandler);

            // setup our interrupt port (on-board button)
            InterruptPort button = new InterruptPort(Pins.ONBOARD_SW1, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeLow);

            // assign our interrupt handler
            button.OnInterrupt += new NativeEventHandler(button_OnInterrupt);
        }

        private SerialPort createSerialPortWithName(string name)
        {
            SerialPort port = new SerialPort(name, 9600);
            port.Parity = Parity.None;
            port.Parity = Parity.None;
            port.StopBits = StopBits.One;
            port.DataBits = 8;
            port.Handshake = Handshake.None;

            return port;
        }

        // the interrupt handler for the button
        void button_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            if (upstreamMQTT != null)
            {
                upstreamMQTT.UnsubscribeFromEvents();
                upstreamMQTT.Disconnect();
                upstreamMQTT = null;
                NDLogger.Log("MQTT connection canceled", LogLevel.Verbose);
            }
            else
            {
                startMQTT();
            }
        }

        void startMQTT()
        {
            int returnCode = 0;
            IPHostEntry hostEntry = null;

            try
            {
                hostEntry = Dns.GetHostEntry("ec2-52-29-5-113.eu-central-1.compute.amazonaws.com");
            }
            catch (SocketException se)
            {
                NDLogger.Log("Socket exception " + se, LogLevel.Error);
                return;
            }
            catch (ArgumentException ae)
            {
                NDLogger.Log("Argument exception" + ae, LogLevel.Error);
                return;
            }

            upstreamMQTT = new MQTTCloudPlatform();
            returnCode = upstreamMQTT.Connect(hostEntry, "mkresz", "qwe12ASD", 1883);

            if (returnCode == 0)
            {
                NDLogger.Log("Connected to MQTT", LogLevel.Verbose);
            }
            else
            {
                NDLogger.Log("Connection to MQTT failed!", LogLevel.Error);
                upstreamMQTT = null;
                return;
            }

            returnCode = upstreamMQTT.SubscribeToEvents(new int[] { 0 }, new String[] { "mkresz/sensors" });

            if (returnCode == 0)
            {
                NDLogger.Log("Subscribed", LogLevel.Verbose);
            }
            else
            {
                NDLogger.Log("Subscription failed with errorCode: " + returnCode, LogLevel.Error);
            }
        }

        void ReceivedRemoteFrameHandler(object sender, ReceivedRemoteFrameEventArgs e)
        {
            CoreCommunication.DIOADCRx16IndicatorFrame frame = (CoreCommunication.DIOADCRx16IndicatorFrame)e.Frame;
            double analogSample = frame.AnalogSampleData[0];
            double temperatureCelsius = ((analogSample / 1023.0 * 3.3) - 0.5) * 100.0;
            NDLogger.Log("Temperature " + temperatureCelsius + " Celsius" + " sample " + analogSample, LogLevel.Info);

            if (upstreamMQTT != null)
            {
                upstreamMQTT.PostEvent(new CLEvent((int)CLEventType.CLTemperatureReadingEventType, temperatureCelsius));
            }
        }

        void FrameDroppedByChecksumHandler(object sender, FrameDroppedByChecksumEventArgs e)
        {
            NDLogger.Log("Frame dropped because of checksum:", LogLevel.Error);
            logBytesRead(e.RawBytes);
        }

        void BytesReadFromSerialHandler(object sender, BytesReadFromSerialEventArgs e)
        {
            NDLogger.Log("Bytes read from serial:", LogLevel.Verbose);
            logBytesRead(e.RawBytes);
        }


        void logBytesRead(byte[] bytes)
        {
            string log = "";
            for (int i = 0; i < bytes.Length; ++i)
            {
                log += ByteToHex(bytes[i]) + " ";
            }
            NDLogger.Log(log, LogLevel.Verbose);
        }

        string ByteToHex(byte b)
        {
            const string hex = "0123456789ABCDEF";
            int lowNibble = b & 0x0F;
            int highNibble = (b & 0xF0) >> 4;
            string s = new string(new char[] { hex[highNibble], hex[lowNibble] });
            return s;
        }
    }
}
