using System;

namespace crozone.LinuxGpio
{
    public enum GpioDirection
    {
        Input,
        Output
    }

    public static class GpioDirectionExtensions
    {
        public static string ToDirectionString(this GpioDirection direction, bool? initialLevel = null)
        {
            switch(direction)
            {
                case GpioDirection.Input:
                    return Constants.GpioDirectionInputValue;
                case GpioDirection.Output:
                    if (initialLevel.HasValue)
                    {
                        return initialLevel.Value ? Constants.GpioDirectionOutputHighValue : Constants.GpioDirectionOutputLowValue;
                    }
                    else
                    {
                        return Constants.GpioDirectionOutputValue;
                    }
                default:
                    throw new InvalidOperationException($"Invalid direction value {direction}");

            }
        }
    }
}
