using System;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public interface IInputPin : IDisposable
    {
        int Pin { get; }
        string Name { get; }
        TimeSpan DebounceTime { get; }
        GpioDirection Direction { get; }
        bool ActiveLow { get; set; }
        bool Value { get;}
        Task WaitForSteadyState(bool state, CancellationToken cancellationToken);
    }
}
