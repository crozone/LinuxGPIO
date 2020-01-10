using System;
using System.Collections.Generic;
using System.Text;

namespace crozone.LinuxGpio
{
    public class PinValueChangedEventArgs : EventArgs
    {
        public PinValueChangedEventArgs(bool value)
        {
            Value = value;
        }

        public bool Value { get; }
    }
}
