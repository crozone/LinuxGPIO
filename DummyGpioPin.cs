using crozone.AsyncResetEvents;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public class DummyGpioPin : IGpioPin
    {
        private readonly TimeSpan defaultAssertionTime = TimeSpan.FromMilliseconds(50);
        private readonly TimeSpan defaultDeassertionTime = TimeSpan.FromMilliseconds(50);
        private readonly TimeSpan defaultDebounceTime = TimeSpan.FromMilliseconds(5);

        private bool activeLow;
        private bool value;

        private GpioDirection direction;
        private AsyncAutoResetEvent pinChangeEvent;
        private bool isDisposed;

        public DummyGpioPin(
            string name,
            GpioDirection? direction,
            bool? activeLow,
            bool? initialValue,
            TimeSpan? assertionTime,
            TimeSpan? deassertionTime,
            TimeSpan? debounceTime)
        {
            this.Name = name;
            this.direction = direction ?? GpioDirection.Input;
            this.value = initialValue ?? false;
            this.AssertionTime = assertionTime ?? defaultAssertionTime;
            this.DeassertionTime = deassertionTime ?? defaultDeassertionTime;
            this.DebounceTime = debounceTime ?? defaultDebounceTime;

            this.pinChangeEvent = new AsyncAutoResetEvent();
            this.isDisposed = false;
        }

        public int Pin => -1;
        public string Name { get; }

        public TimeSpan AssertionTime { get; }
        public TimeSpan DeassertionTime { get; }
        public TimeSpan DebounceTime { get; }

        public GpioDirection Direction {
            get {
                ThrowIfDisposed();
                return direction;
            }
            set {
                ThrowIfDisposed();
                direction = value;
            }
        }

        public bool Value {
            get {
                ThrowIfDisposed();
                return value;
            }
            set {
                ThrowIfDisposed();

                if(value != this.value)
                {
                    pinChangeEvent.Set();
                }

                this.value = value;
            }
        }

        public bool ActiveLow {
            get {
                ThrowIfDisposed();
                return activeLow;
            }
            set {
                ThrowIfDisposed();
                activeLow = value;
            }
        }

        public Task Pulse(CancellationToken cancellationToken)
        {
            return Pulse(1, cancellationToken);
        }

        public async Task Pulse(double assertionTimeMultiplier, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                Value = true;
                await Task.Delay(TimeSpan.FromTicks((int)Math.Round(AssertionTime.Ticks * assertionTimeMultiplier)), cancellationToken);
            }
            finally
            {
                Value = false;
            }

            await Task.Delay(DeassertionTime, cancellationToken);
        }

        public override string ToString()
        {
            return $"[Dummy] {Name}";
        }

        public void Dispose()
        {
            isDisposed = true;
            pinChangeEvent = null;
        }

        public async Task WaitForSteadyState(bool state, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            while(value != state)
            {
                await pinChangeEvent.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Throw an ObjectDisposedException if the object is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(DummyGpioPin));
        }
    }
}
