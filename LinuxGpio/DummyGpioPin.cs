using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public class DummyGpioPin : IInputPin, IOutputPin
    {
        private GpioDirection? initialDirection;
        private bool? initialActiveLow;
        private bool? initialValue;
        private volatile bool isDisposed;
        private bool isOpened;

        private GpioDirection realGpioDirection = GpioDirection.Input;
        private bool realActiveLow = false;
        private bool realValue = false;

        private readonly object pinChangeRegistrationScopesLock = new object();
        private readonly List<PinChangeRegistrationScope> pinChangeRegistrationScopes = new List<PinChangeRegistrationScope>();

        private readonly object pinEventRaisingTaskLock = new object();
        private Task pinEventRaisingTask = null;
        private CancellationTokenSource pinEventRaisingTaskCancellationSource = null;

        public int Pin { get; private set; }
        public string Name { get; private set; }

        public event EventHandler<PinValueChangedEventArgs> ValueChanged;

        /// <summary>
        /// Represents a controller for GPIO control on any Linux system with 
        /// GPIO hardware support enabled.
        /// </summary>
        ///
        public DummyGpioPin(
            int pin,
            string name)
        {
            this.Pin = pin;
            this.Name = name;

            isDisposed = false;
        }

        public void Open()
        {
            ThrowIfDisposed();

            if (isOpened)
            {
                throw new InvalidOperationException("Pin is already open");
            }

            isOpened = true;

            if (initialActiveLow.HasValue)
            {
                SetActiveLowInternal(initialActiveLow.Value);
            }

            if (initialDirection.HasValue)
            {
                SetDirectionInternal(initialDirection.Value, initialValue);
            }

            // We don't have to set Value to initialValue here because setting Direction to
            // output will automatically set the value to the initialValue.
            //
            EnableRaisingEventsInternal(enableRaisingEvents);
        }

        /// <summary>
        /// Gets or sets the GPIO pin's direction.
        /// </summary>
        public GpioDirection Direction {
            get {
                ThrowIfDisposed();

                if (!isOpened)
                {
                    return initialDirection ?? GpioDirection.Input;
                }

                return GetDirectionInternal();
            }
            set {
                ThrowIfDisposed();

                initialDirection = value;

                if (isOpened)
                {
                    SetDirectionInternal(value, initialValue);
                }
            }
        }

        private GpioDirection GetDirectionInternal()
        {
            return realGpioDirection;
        }

        private void SetDirectionInternal(GpioDirection direction, bool? initialValue = null)
        {
            realGpioDirection = direction;

            if (initialValue != null && direction == GpioDirection.Output)
            {
                SetValueInternal(initialValue.Value);
            }
        }

        public bool ActiveLow {
            get {
                ThrowIfDisposed();

                if (!isOpened)
                {
                    return initialActiveLow ?? false;
                }

                return GetActiveLowInternal();
            }
            set {
                ThrowIfDisposed();

                initialActiveLow = value;

                if (isOpened)
                {
                    SetActiveLowInternal(value);
                }
            }
        }

        private bool GetActiveLowInternal()
        {
            return realActiveLow;
        }

        private void SetActiveLowInternal(bool value)
        {
            realActiveLow = value;
        }

        /// <summary>
        /// Gets or sets the GPIO pin's value.
        /// If the pin is set to Output, this can still be set, and the value will be cached.
        /// When the pin is next set to output, the cached value will be set as the initial state
        /// of the pin.
        /// </summary>
        public bool Value {
            get {
                ThrowIfDisposed();

                if (!isOpened)
                {
                    return initialValue ?? false;
                }

                return GetValueInternal();
            }
            set {
                ThrowIfDisposed();

                initialValue = value;

                if (isOpened)
                {
                    SetValueInternal(value);
                }
            }
        }

        private bool GetValueInternal()
        {
            return realValue ^ realActiveLow;
        }

        private void SetValueInternal(bool value)
        {
            if (Direction == GpioDirection.Output)
            {
                bool oldValue = realValue;
                bool newValue = value ^ realActiveLow;
                realValue = newValue;

                if (oldValue != newValue)
                {
                    NotifyAllScopesOfPinChange(value);
                }
            }
            else
            {
                throw new InvalidOperationException($"Cannot set value for an {GpioDirection.Input} direction pin");
            }
        }

        private bool enableRaisingEvents;
        public bool EnableRaisingEvents {
            get {
                ThrowIfDisposed();

                return enableRaisingEvents;
            }
            set {
                ThrowIfDisposed();

                enableRaisingEvents = value;

                if (isOpened)
                {
                    EnableRaisingEventsInternal(value);
                }
            }
        }

        private void EnableRaisingEventsInternal(bool enableEvents)
        {
            if (enableEvents)
            {
                // Start the event raising task
                lock (pinEventRaisingTaskLock)
                {
                    if (pinEventRaisingTask == null)
                    {
                        pinEventRaisingTaskCancellationSource = new CancellationTokenSource();
                        pinEventRaisingTask = Task.Run(() => PinChangedEventRaisingTask(pinEventRaisingTaskCancellationSource.Token), pinEventRaisingTaskCancellationSource.Token);
                    }
                }
            }
            else
            {
                // Stop the event raising task
                lock (pinEventRaisingTaskLock)
                {
                    if (pinEventRaisingTask != null)
                    {
                        pinEventRaisingTaskCancellationSource.Cancel();

                        try
                        {
                            pinEventRaisingTask.GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {

                        }

                        pinEventRaisingTaskCancellationSource = null;
                        pinEventRaisingTask = null;
                    }
                }
            }
        }

        private async Task PinChangedEventRaisingTask(CancellationToken cancellationToken)
        {
            using (PinChangeRegistrationScope scope = CreateAndRegisterPinChangedScope())
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bool value = await scope.WhenPinChange(Timeout.InfiniteTimeSpan, cancellationToken);

                    try
                    {
                        ValueChanged?.Invoke(this, new PinValueChangedEventArgs(this, value));
                    }
                    catch
                    {

                    }
                }
            }
        }

        private PinChangeRegistrationScope CreateAndRegisterPinChangedScope()
        {
            lock (pinChangeRegistrationScopesLock)
            {
                PinChangeRegistrationScope newScope = new PinChangeRegistrationScope(this);
                pinChangeRegistrationScopes.Add(newScope);
                return newScope;
            }
        }

        private void DeregisterPinChangeScope(PinChangeRegistrationScope scope)
        {
            lock (pinChangeRegistrationScopesLock)
            {
                pinChangeRegistrationScopes.Remove(scope);
            }
        }

        private class PinChangeRegistrationScope : IDisposable
        {
            private readonly DummyGpioPin pin;
            private readonly AwaitableQueue<bool> changesQueue = new AwaitableQueue<bool>();

            public PinChangeRegistrationScope(DummyGpioPin pin)
            {
                this.pin = pin;
            }

            public void Dispose()
            {
                pin.DeregisterPinChangeScope(this);
                changesQueue.Dispose();
            }

            public void NotifyOfPinChange(bool pinValue)
            {
                changesQueue.Enqueue(pinValue);
            }

            public bool WaitForPinChange(TimeSpan timeSpan, CancellationToken cancellationToken)
            {
                return changesQueue.WaitAndDequeue(timeSpan, cancellationToken);
            }

            public Task<bool> WhenPinChange(TimeSpan timeSpan, CancellationToken cancellationToken)
            {
                return changesQueue.WhenDequeue(timeSpan, cancellationToken);
            }
        }

        private void NotifyAllScopesOfPinChange(bool pinValue)
        {
            lock (pinChangeRegistrationScopesLock)
            {
                foreach (var scope in pinChangeRegistrationScopes)
                {
                    scope.NotifyOfPinChange(pinValue);
                }
            }
        }

        public void Pulse()
        {
            Pulse(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
        }

        public void Pulse(TimeSpan assertionDuration, TimeSpan deassertionDuration)
        {
            ThrowIfDisposed();

            try
            {
                Value = true;
                Thread.Sleep(assertionDuration);
            }
            finally
            {
                Value = false;
            }

            Thread.Sleep(deassertionDuration);
        }

        public Task PulseAsync(CancellationToken cancellationToken)
        {
            return PulseAsync(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), cancellationToken);
        }

        public async Task PulseAsync(TimeSpan assertionDuration, TimeSpan deassertionDuration, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                Value = true;
                await Task.Delay(assertionDuration, cancellationToken);
            }
            finally
            {
                Value = false;
            }

            await Task.Delay(deassertionDuration, cancellationToken);
        }

        public override string ToString()
        {
            return $"[{Pin}] {Name}";
        }

        public void Dispose()
        {
            isDisposed = true;

            EnableRaisingEventsInternal(false);

            // Dispose all pin change scopes.
            // This will deregister all the pins from changes within this class,
            // and in turn stop the pin change monitoring task.

            lock (pinChangeRegistrationScopesLock)
            {
                foreach (var scope in pinChangeRegistrationScopes)
                {
                    scope.Dispose();
                }
            }
        }

        /// <summary>
        /// Throw an ObjectDisposedException if the object is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(LinuxGpioPin));
        }

        public void WaitForValue(bool state)
        {
            WaitForValue(state, Timeout.InfiniteTimeSpan);
        }

        public void WaitForValue(bool state, TimeSpan timeout)
        {
            ThrowIfDisposed();

            using (PinChangeRegistrationScope scope = CreateAndRegisterPinChangedScope())
            {
                while (true)
                {
                    bool value = scope.WaitForPinChange(Timeout.InfiniteTimeSpan, CancellationToken.None);

                    if (value == state)
                    {
                        break;
                    }
                }
            }
        }

        public Task WhenValue(bool state, CancellationToken cancellationToken)
        {
            return WhenValue(state, Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public async Task WhenValue(bool state, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (PinChangeRegistrationScope scope = CreateAndRegisterPinChangedScope())
            {
                while (true)
                {
                    bool value = await scope.WhenPinChange(Timeout.InfiniteTimeSpan, cancellationToken);

                    if (value == state)
                    {
                        break;
                    }
                }
            }
        }

        public bool WaitForChange()
        {
            return WaitForChange(Timeout.InfiniteTimeSpan);
        }

        public bool WaitForChange(TimeSpan timeout)
        {
            ThrowIfDisposed();

            using (PinChangeRegistrationScope scope = CreateAndRegisterPinChangedScope())
            {
                return scope.WaitForPinChange(Timeout.InfiniteTimeSpan, CancellationToken.None);
            }
        }

        public Task<bool> WhenChanged(CancellationToken cancellationToken)
        {
            return WhenChanged(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public async Task<bool> WhenChanged(TimeSpan timeout, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (PinChangeRegistrationScope scope = CreateAndRegisterPinChangedScope())
            {
                return await scope.WhenPinChange(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }
}
