using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Net.NetworkInformation;

using CloudLib;

namespace NetduinoPlusTemperatureReading
{
    class NDMQTT : MQTTCloudPlatform
    {
        public NDMQTT()
        {
            this.TopicFromEventType = topicFromEventType;
            this.clientID = ClientID;
        }

        private string topicFromEventType(int type)
        {
            String topic = "";
            switch (type)
            {
                case (int)CLEventType.CLTemperatureReadingEventType:
                    topic = NDConfiguration.DefaultConfiguration.MQTT.SensorDataTopic;
                    break;

                case (int)CLEventType.CLLogMessageEventType:
                    topic = NDConfiguration.DefaultConfiguration.MQTT.LogTopic;
                    break;

                default:
                    break;
            }

            return topic;
        }

        private string ClientID
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
    }
}
