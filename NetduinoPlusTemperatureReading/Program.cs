using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO.Ports;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;

using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

using CloudLib;
using Logger;

namespace NetduinoPlusTemperatureReading
{
    public class Program
    {
        static XbeeDevice xbeeCoordinator;
        static ICloudPlatform upstreamMQTT;
        static OutputPort onboardLED = new OutputPort(Pins.ONBOARD_LED, false);

        public static SerialPort createSerialPortWithName(string name)
        {
            SerialPort port = new SerialPort(name, 9600);
            port.Parity = Parity.None;
            port.Parity = Parity.None;
            port.StopBits = StopBits.One;
            port.DataBits = 8;
            port.Handshake = Handshake.None;

            return port;
        }

        public static void Main()
        {
            NDLogger.SetLogLevel(LogLevel.Verbose);
            NDLogger.Log("Program started!");

            xbeeCoordinator = new XbeeDevice(createSerialPortWithName("COM1"));

            xbeeCoordinator.BytesReadFromSerial += new BytesReadFromSerialEventHandler(BytesReadFromSerialHandler);
            xbeeCoordinator.FrameDroppedByChecksum += new FrameDroppedByChecksumEventHandler(FrameDroppedByChecksumHandler);
            xbeeCoordinator.ReceivedRemoteFrame += new ReceivedRemoteFrameEventHandler(ReceivedRemoteFrameHandler);

            waitForEthernetSetUp();

            // setup our interrupt port (on-board button)
            InterruptPort button = new InterruptPort(Pins.ONBOARD_SW1, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeLow);

            // assign our interrupt handler
            button.OnInterrupt += new NativeEventHandler(button_OnInterrupt);

            Thread.Sleep(Timeout.Infinite);
        }

        static void waitForEthernetSetUp()
        {
            NetworkInterface NI = NetworkInterface.GetAllNetworkInterfaces()[0];

            if (!NI.IsDhcpEnabled || NI.IPAddress == "0.0.0.0")
            {
                NI.EnableDhcp();
                NI.RenewDhcpLease();
            }

            int sec = 0;
            while (NI.IPAddress == "0.0.0.0")
            {
                NDLogger.Log("Waiting for DHCP to set up. Elapsed time: " + sec, LogLevel.Verbose);
                onboardLED.Write(true);
                Thread.Sleep(5000);
                sec += 5;
                NI = NetworkInterface.GetAllNetworkInterfaces()[0];
            }

            onboardLED.Write(false);
            NDLogger.Log("Ethernet IP " + NI.IPAddress.ToString(), LogLevel.Verbose);
        }

        // the interrupt handler for the button
        static void button_OnInterrupt(uint data1, uint data2, DateTime time)
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

        static void startMQTT()
        {
            int returnCode = 0;
                IPHostEntry hostEntry = null;

                try
                {
                    hostEntry = Dns.GetHostEntry("192.168.0.14");
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

        static void ReceivedRemoteFrameHandler(object sender, ReceivedRemoteFrameEventArgs e)
        {
            CoreCommunication.DIOADCRx16IndicatorFrame frame = (CoreCommunication.DIOADCRx16IndicatorFrame)e.Frame;
            double analogSample = frame.AnalogSampleData[0];
            double temperatureCelsius = ((analogSample / 1023.0 * 3.3) - 0.5) * 100.0;
            NDLogger.Log("Temperature " + temperatureCelsius + " Celsius" + " sample " + analogSample, LogLevel.Info);
        }

        static void FrameDroppedByChecksumHandler(object sender, FrameDroppedByChecksumEventArgs e)
        {
            NDLogger.Log("Frame dropped because of checksum:", LogLevel.Error);
            logBytesRead(e.RawBytes);
        }

        static void BytesReadFromSerialHandler(object sender, BytesReadFromSerialEventArgs e)
        {
            NDLogger.Log("Bytes read from serial:", LogLevel.Verbose);
            logBytesRead(e.RawBytes);
        }

        
        static void logBytesRead(byte[] bytes)
        {
            string log = "";
            for (int i = 0; i < bytes.Length; ++i)
            {
                log += ByteToHex(bytes[i]) + " ";
            }
            NDLogger.Log(log, LogLevel.Verbose);
        }

        static string ByteToHex(byte b)
        {
            const string hex = "0123456789ABCDEF";
            int lowNibble = b & 0x0F;
            int highNibble = (b & 0xF0) >> 4;
            string s = new string(new char[] { hex[highNibble], hex[lowNibble] });
            return s;
        }
    }
}
