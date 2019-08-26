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
                    return "in";
                case GpioDirection.Output:
                    if (initialLevel.HasValue)
                    {
                        return initialLevel.Value ? "high" : "low";
                    }
                    else
                    {
                        return "out";
                    }
                default:
                    throw new InvalidOperationException("Invalid direction value");

            }
        }
    }
}
