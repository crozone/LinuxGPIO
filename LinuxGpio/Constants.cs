using System;
using System.Collections.Generic;
using System.Text;

namespace crozone.LinuxGpio
{
    internal static class Constants
    {
        public const string GpioDirectionInputValue = "in";
        public const string GpioDirectionOutputValue = "out";
        public const string GpioDirectionOutputHighValue = "high";
        public const string GpioDirectionOutputLowValue = "low";

        public const string GpioActiveLowFalseValue = "0";
        public const string GpioActiveLowTrueValue = "1";

        public const string GpioValueFalseValue = "0";
        public const string GpioValueTrueValue = "1";
    }
}
