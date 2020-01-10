using System;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    /// <summary>
    /// Provides control of GPIO hardware
    /// </summary>
    public interface IGpioPin : IDisposable
    {
        int Pin { get; }
        string Name { get; }
        GpioDirection Direction { get; set; }
        bool ActiveLow { get; set; }
        bool Value { get; set; }
        bool EnableRaisingEvents { get; set; }
        event EventHandler<PinValueChangedEventArgs> ValueChanged;

        void Open();
        void WaitForValue(bool value);
        void WaitForValue(bool value, TimeSpan timeout);
        Task WhenValue(bool value, CancellationToken cancellationToken);
        Task WhenValue(bool value, TimeSpan timeout, CancellationToken cancellationToken);

        bool WaitForChange();
        bool WaitForChange(TimeSpan timeout);
        Task<bool> WhenChanged(CancellationToken cancellationToken);
        Task<bool> WhenChanged(TimeSpan timeout, CancellationToken cancellationToken);
    }
}
