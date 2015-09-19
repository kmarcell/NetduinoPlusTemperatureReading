using System;
using Microsoft.SPOT;

namespace Logger
{
    public enum LogLevel : int
    {
        Info = 1,
        Error = 2,
        Warning = 3,
        Verbose = 4,
    }

    class NDLogger
    {
        private static int LOG_LEVEL = 1;

        public static void Log(string text, LogLevel level = LogLevel.Info)
        {
            if (LOG_LEVEL > 0 && (int)level <= LOG_LEVEL)
            {
                Debug.Print(text);
            }
        }

        public static void SetLogLevel(LogLevel logLevel)
        {
            LOG_LEVEL = (int)logLevel;
        }

        public static void EnableLogging()
        {
            LOG_LEVEL = 1;
        }

        public static void DisableLogging()
        {
            LOG_LEVEL = 0;
        }
    }
}
