using System;
using System.Net;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;

namespace NetduinoPlusTemperatureReading
{
    public class Program
    {
        static IApplication application;

        public static void Main()
        {
            application = new Application();
            application.applicationWillStart();

            waitForEthernetSetUp();
            setupBroadcast();

            application.didFinishLaunching();

            Thread.Sleep(Timeout.Infinite);
        }

        static void waitForEthernetSetUp()
        {
            NetworkInterface NI = NetworkInterface.GetAllNetworkInterfaces()[0];

            if (!NI.IsDhcpEnabled)
            {
                NI.EnableDhcp();
                
            }

            if (NI.IPAddress == "0.0.0.0") {
                NI.RenewDhcpLease();
            }

            int sec = 0;
            while (NI.IPAddress == "0.0.0.0")
            {
                Thread.Sleep(5000);
                sec += 5;
                NI = NetworkInterface.GetAllNetworkInterfaces()[0];
            }
        }

        static void setupBroadcast()
        {
            NDBroadcastAddress.sharedInstance.startBroadcast("192.168.0.255");
        }
    }
}
