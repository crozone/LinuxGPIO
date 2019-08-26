using System;

namespace crozone.LinuxGpio
{
    public class LinuxGpioFactory : IGpioFactory
    {
        public IInputPin GetInputPin(
            int pin,
            string name,
            bool export,
            bool unexport,
            bool? activeLow,
            TimeSpan? debounceTime)
        {
            return GetPin(
                pin: pin,
                name: name,
                export: export,
                unexport: unexport,
                direction: GpioDirection.Input,
                activeLow: activeLow,
                initialValue: null,
                assertionTime: null,
                deassertionTime: null,
                debounceTime: debounceTime);
        }

        public IOutputPin GetOutputPin(
            int pin,
            string name,
            bool export,
            bool unexport,
            bool? activeLow,
            bool? initialValue,
            TimeSpan? assertionTime,
            TimeSpan? deassertionTime)
        {
            return GetPin(
                pin: pin,
                name: name,
                export: export,
                unexport: unexport,
                direction: GpioDirection.Output,
                activeLow: activeLow,
                initialValue: initialValue,
                assertionTime: assertionTime,
                deassertionTime: deassertionTime,
                debounceTime: null);
        }

        private IGpioPin GetPin(
            int pin,
            string name,
            bool export,
            bool unexport,
            GpioDirection? direction,
            bool? activeLow,
            bool? initialValue,
            TimeSpan? assertionTime,
            TimeSpan? deassertionTime,
            TimeSpan? debounceTime)
        {
            // Create the pin
            //
            LinuxGpioPin newPin = new LinuxGpioPin(
                pin: pin,
                name: name,
                export: export,
                unexport: unexport,
                direction: direction,
                activeLow: activeLow,
                initialValue: initialValue,
                assertionTime: assertionTime,
                deassertionTime: deassertionTime,
                debounceTime: debounceTime);

            return newPin;
        }
    }
}
