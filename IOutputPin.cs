﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public interface IOutputPin : IDisposable
    {
        int Pin { get; }
        string Name { get; }
        TimeSpan AssertionTime { get; }
        GpioDirection Direction { get; }
        bool ActiveLow { get; set; }
        bool Value { get; set; }

        /// <summary>
        /// Asserts the pin for its assertion time,
        /// and then deasserts the pin for its assertion time.
        /// </summary>
        /// <returns></returns>
        Task Pulse(CancellationToken cancellationToken);

        /// <summary>
        /// Asserts the pin for a multiple of its assertion time,
        /// and then deasserts the pin for its deassertion time.
        /// </summary>
        /// <returns></returns>
        Task Pulse(double assertionTimeMultiplier, CancellationToken cancellationToken);
    }
}
