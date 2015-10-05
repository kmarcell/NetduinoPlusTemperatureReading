using System;
using Microsoft.SPOT;

namespace NetduinoPlusTemperatureReading
{
    interface IApplication
    {
        void applicationWillStart();
        void didFinishLaunching();
    }
}
