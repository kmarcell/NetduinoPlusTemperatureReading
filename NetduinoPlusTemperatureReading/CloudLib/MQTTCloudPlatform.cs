using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;

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

        public int Connect(IPHostEntry host, string username, string password, int port = 1883)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(new IPEndPoint(host.AddressList[0], port));
            }
            catch (SocketException SE)
            {
                NDLogger.Log("Socket Error: " + SE.ErrorCode, LogLevel.Error);
                return SE.ErrorCode;
            }

            int returnCode = NetduinoMQTT.ConnectMQTT(socket, this.ClientID, 20, true, username, password);
            if (returnCode != 0)
            {
                Debug.Print("MQTT connection Error: " + returnCode.ToString());
                return returnCode;
            }

            Timer pingTimer = new Timer(new TimerCallback(PingServer), null, 1000, 10000);

            // Setup and start a new thread for the listener
            listenerThread = new Thread(mylistenerThread);
            listenerThread.Start();

            return 0;
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
            NetduinoMQTT.PublishMQTT(socket, TopicFromEventType(e.EventType), "{ value : " + e.EventValue + " }");
            return 0;
        }

        /** Private **/

        protected string ClientID
        {
            get
            {
                byte[] PhysicalAddress = Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].PhysicalAddress;
                string MACaddress = new string(System.Text.Encoding.UTF8.GetChars(PhysicalAddress));

                return MACaddress;
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
                    topic = "TemperatureReading";
                    break;

                default:
                    break;
            }

            return topic;
        }

    }
}
