using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO.Ports;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

namespace NetduinoPlusTemperatureReading
{
    public class Program
    {
        static XbeeDevice xbeeCoordinator;

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
            xbeeCoordinator = new XbeeDevice(createSerialPortWithName("COM1"));

            xbeeCoordinator.BytesReadFromSerial += new BytesReadFromSerialEventHandler(BytesReadFromSerialHandler);
            xbeeCoordinator.FrameDroppedByChecksum += new FrameDroppedByChecksumEventHandler(FrameDroppedByChecksumHandler);
            xbeeCoordinator.ReceivedRemoteFrame += new ReceivedRemoteFrameEventHandler(ReceivedRemoteFrameHandler);

            Debug.Print("Program started!");
            Thread.Sleep(Timeout.Infinite);
        }

        static Boolean isLogging()
        {
            return false;
        }

        static void ReceivedRemoteFrameHandler(object sender, ReceivedRemoteFrameEventArgs e)
        {
            CoreCommunication.DIOADCRx16IndicatorFrame frame = (CoreCommunication.DIOADCRx16IndicatorFrame)e.Frame;
            double analogSample = frame.AnalogSampleData[0];
            double temperatureCelsius = ((analogSample / 1023.0 * 3.3) - 0.5) * 100.0;
            Debug.Print("Temperature " + temperatureCelsius + " Celsius" + " sample " + analogSample);
        }

        static void FrameDroppedByChecksumHandler(object sender, FrameDroppedByChecksumEventArgs e)
        {
            if (isLogging())
            {
                Debug.Print("Frame dropped because of checksum:");
                logBytesRead(e.RawBytes);
                    
            }
        }

        static void BytesReadFromSerialHandler(object sender, BytesReadFromSerialEventArgs e)
        {
            if (isLogging())
            {
                Debug.Print("Bytes read from serial:");
                logBytesRead(e.RawBytes);
            }
        }

        
        static void logBytesRead(byte[] bytes)
        {
            string log = "";
            for (int i = 0; i < bytes.Length; ++i)
            {
                log += ByteToHex(bytes[i]) + " ";
            }
            Debug.Print(log);
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
