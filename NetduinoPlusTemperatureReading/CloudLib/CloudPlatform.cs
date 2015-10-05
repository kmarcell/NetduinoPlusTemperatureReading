using System;
using System.Net;

namespace CloudLib
{
    public enum CLEventType : int
    {
        CLTemperatureReadingEventType,
    }

    public class CLEvent
    {
        public int EventType;
        public double EventValue;

        public CLEvent(int eventType, double eventValue)
        {
            this.EventType = eventType;
            this.EventValue = eventValue;
        }
    }

    public interface ICloudPlatform
    {
        int Connect(IPHostEntry host, String username, String password, int port);
        int Disconnect();

        int SubscribeToEvents(int[] topicQoS, String[] subTopics);
        int UnsubscribeFromEvents();

        int PostEvent(CLEvent e);
    }
}
