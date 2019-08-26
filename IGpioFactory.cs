using System;

namespace crozone.LinuxGpio
{
    public interface IGpioFactory
    {
        IInputPin GetInputPin(
            int pin,
            string name,
            bool export,
            bool unexport,
            bool? activeLow,
            TimeSpan? debounceTime);

        IOutputPin GetOutputPin(
            int pin,
            string name,
            bool export,
            bool unexport,
            bool? activeLow,
            bool? initialValue,
            TimeSpan? assertionTime,
            TimeSpan? deassertionTime);
    }
}
