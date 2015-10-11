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
        private MQTTCloudPlatform upstreamMQTT;
        private OutputPort onboardLED;

        public void applicationWillStart()
        {
            // Logging
            NDLogger.RemoveLoggers();
            NDLogger.AddLogger(new NDTTYLogger());
            NDLogger.SetLogLevel(LogLevel.Verbose);

            NDLogger.Log("Program started!");
            NDLogger.Log("Waiting for DHCP to set up.", LogLevel.Verbose);

            if (onboardLED == null)
            {
                onboardLED = new OutputPort(Pins.ONBOARD_LED, false);
            }
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
                try
                {
                    upstreamMQTT.UnsubscribeFromEvents();
                }
                catch
                {
                }

                upstreamMQTT.Disconnect();
                upstreamMQTT = null;
                NDLogger.Log("MQTT connection cancelled", LogLevel.Verbose);
            }
            else
            {
                try
                {
                    startMQTT();
                }
                catch
                {
                }
            }
        }

        void startMQTT()
        {
            IPHostEntry hostEntry = null;
            try
            {
                hostEntry = Dns.GetHostEntry(Configuration.MQTT.HostName);
            }
            catch (SocketException se)
            {
                NDLogger.Log("Unable to get host entry by DNS error: " + se, LogLevel.Error);
                return;
            }

            upstreamMQTT = new NDMQTT();

            int returnCode = upstreamMQTT.Connect(hostEntry, Configuration.MQTT.UserName, Configuration.MQTT.Password, Configuration.MQTT.HostPort);

            if (returnCode == 0)
            {
                NDLogger.AddLogger(new MQTTLogger(upstreamMQTT));
            }
            else
            {
                upstreamMQTT = null;
                return;
            }

            upstreamMQTT.SubscribeToEvents(new int[] { 0 }, new String[] { Configuration.MQTT.SensorDataTopic });
        }

        public NDConfiguration Configuration
        {
            get { return NDConfiguration.DefaultConfiguration; }
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
