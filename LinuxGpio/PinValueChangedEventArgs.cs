using System;
using System.Collections.Generic;
using System.Text;

namespace crozone.LinuxGpio
{
    public class PinValueChangedEventArgs : EventArgs
    {
        public PinValueChangedEventArgs(IGpioPin pin, bool value)
        {
            Pin = pin;
            Value = value;
        }

        public IGpioPin Pin { get; }
        public bool Value { get; }
    }
}
