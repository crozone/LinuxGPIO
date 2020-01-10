using System;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public interface IOutputPin : IGpioPin
    {
        /// <summary>
        /// Asserts the pin for 1ms,
        /// and then deasserts the pin for 1ms.
        /// </summary>
        /// <returns></returns>
        void Pulse();

        /// <summary>
        /// Asserts the pin for assertion duration,
        /// and then deasserts the pin deassertion duration.
        /// </summary>
        /// <returns></returns>
        void Pulse(TimeSpan assertionDuration, TimeSpan deassertionDuration);

        /// <summary>
        /// Asserts the pin for 1ms,
        /// and then deasserts the pin for 1ms.
        /// </summary>
        /// <returns></returns>
        Task PulseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Asserts the pin for assertion duration,
        /// and then deasserts the pin deassertion duration.
        /// </summary>
        /// <returns></returns>
        Task PulseAsync(TimeSpan assertionDuration, TimeSpan deassertionDuration, CancellationToken cancellationToken);   
    }
}
