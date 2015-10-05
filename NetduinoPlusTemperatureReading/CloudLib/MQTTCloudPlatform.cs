using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Net.NetworkInformation;

using Logger;
using Netduino_MQTT_Client_Library;

namespace CloudLib
{
    class MQTTCloudPlatform : ICloudPlatform
    {
        protected Socket socket;
        protected Thread listenerThread;
        protected int[] topicQoS;
        protected String[] subTopics;

        protected String userName;

        ~MQTTCloudPlatform()
        {
            if (listenerThread != null)
            {
                listenerThread.Abort();
            }
            
            if (socket != null)
            {
                socket.Close();
            }
        }

        public int Connect(IPHostEntry host, String userName, string password, int port = 1883)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool success = TryConnect(socket, new IPEndPoint(host.AddressList[0], port));
            this.userName = userName;

            if (!success)
            {
                socket.Close();
                socket = null;
                NDLogger.Log("Unknown socket error!", LogLevel.Error);
                return Constants.CONNECTION_ERROR;
            }

            int returnCode = NetduinoMQTT.ConnectMQTT(socket, this.ClientID, 20, true, userName, password);
            if (returnCode != Constants.SUCCESS)
            {
                NDLogger.Log("MQTT connection Error: " + returnCode, LogLevel.Error);
                return returnCode;
            }

            Timer pingTimer = new Timer(new TimerCallback(PingServer), null, 1000, 10000);

            // Setup and start a new thread for the listener
            listenerThread = new Thread(mylistenerThread);
            listenerThread.Start();

            return 0;
        }

        bool TryConnect(Socket s, EndPoint ep)
        {
            bool connected = false;
            new Thread(delegate
            {
                try
                {
                    s.Connect(ep);
                    connected = true;
                }
                catch { }

            }).Start();

            int checks = 10;
            while (checks-- > 0 && connected == false) Thread.Sleep(100);
            
            return connected;
        }

        public int Disconnect()
        {
            int returnCode = NetduinoMQTT.DisconnectMQTT(socket);

            socket.Close();
            socket = null;

            return returnCode;
        }

        public int SubscribeToEvents(int[] topicQoS, String[] subTopics)
        {
            this.topicQoS = topicQoS;
            this.subTopics = subTopics;
            int returnCode = NetduinoMQTT.SubscribeMQTT(socket, subTopics, topicQoS, 1);
            return returnCode;
        }

        public int UnsubscribeFromEvents()
        {
            int returnCode = NetduinoMQTT.UnsubscribeMQTT(socket, this.subTopics, this.topicQoS, 1);
            return returnCode;
        }

        public int PostEvent(CLEvent e)
        {
            if (listenerThread == null) { return 1; }

            NetduinoMQTT.PublishMQTT(socket, TopicFromEventType(e.EventType), "" + e.EventValue);
            return 0;
        }

        /** Private **/

        protected string ClientID
        {
            get
            {
                NetworkInterface[] netIF = NetworkInterface.GetAllNetworkInterfaces();

                string macAddress = "";

                // Create a character array for hexidecimal conversion.
                const string hexChars = "0123456789ABCDEF";

                // Loop through the bytes.
                for (int b = 0; b < 6; b++)
                {
                    // Grab the top 4 bits and append the hex equivalent to the return string.
                    macAddress += hexChars[netIF[0].PhysicalAddress[b] >> 4];

                    // Mask off the upper 4 bits to get the rest of it.
                    macAddress += hexChars[netIF[0].PhysicalAddress[b] & 0x0F];

                    // Add the dash only if the MAC address is not finished.
                    if (b < 5) macAddress += "-";
                }

                return macAddress;
            }
        }

        // The function that the timer calls to ping the server
        // Our keep alive is 15 seconds - we ping again every 10. 
        // So we should live forever.
        private void PingServer(object o)
        {
            Debug.Print("pingIT");
            NetduinoMQTT.PingMQTT(socket);
        }

        // The thread that listens for inbound messages
        private void mylistenerThread()
        {
            NetduinoMQTT.listen(socket);
        }

        private String TopicFromEventType(int type)
        {
            String topic = "";
            switch (type)
            {
                case (int)CLEventType.CLTemperatureReadingEventType:
                    topic = "users/" + this.userName + "/sensors";
                    break;

                default:
                    break;
            }

            return topic;
        }

    }
}
