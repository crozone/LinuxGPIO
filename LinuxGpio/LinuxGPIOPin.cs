using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    /// <summary>
    /// Provides userspace control of GPIO hardware pins on any Linux system that has
    /// gpiolib interfaced to sysfs.
    /// </summary>
    public class LinuxGpioPin : IInputPin, IOutputPin
    {
        private static readonly string gpioDirectory = Path.DirectorySeparatorChar + Path.Combine("sys", "class", "gpio");

        private GpioDirection? initialDirection;
        private bool? initialActiveLow;
        private bool? initialValue;
        private volatile bool isDisposed;
        private bool isOpened;

        private readonly object pinChangeRegistrationScopesLock = new object();
        private readonly List<PinChangeRegistrationScope> pinChangeRegistrationScopes = new List<PinChangeRegistrationScope>();
        private Task pinMonitorTask = null;
        private CancellationTokenSource pinMonitorTaskCancellationSource = null;


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
        public LinuxGpioPin(
            int pin,
            string name)
        {
            this.Pin = pin;
            this.Name = name;
            this.UseInotify = PlatformSupportsInotify();

            isDisposed = false;
        }

        public void Open()
        {
            ThrowIfDisposed();

            if (isOpened)
            {
                throw new InvalidOperationException("Pin is already open");
            }

            if (Export)
            {
                if (!TryEnablePin())
                {
                    throw new InvalidOperationException("Could not export the pin");
                }
            }
            else
            {
                EnsurePinIsExported();
            }

            if (UseInotify && !PlatformSupportsInotify())
            {
                throw new InvalidOperationException("Platform does not support inotify for change detection");
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
            string currentDirection = File.ReadAllText(GetDirectionPath());

            if (GpioDirection.Input.ToDirectionString() == currentDirection)
            {
                return GpioDirection.Input;
            }
            else if (GpioDirection.Output.ToDirectionString() == currentDirection)
            {
                return GpioDirection.Output;
            }
            else
            {
                throw new InvalidOperationException($"Pin direction returned {currentDirection}, which appears to be invalid.");
            }
        }

        private void SetDirectionInternal(GpioDirection direction, bool? initialValue = null)
        {
            string directionString = direction.ToDirectionString(initialValue);

            // "sys/class/gpio/gpioXX/direction"
            //
            string pinDirectionPath = GetDirectionPath();

            // Set the direction
            //
            File.WriteAllText(pinDirectionPath, directionString);

            // Set edge detection if the direction is input
            //
            if (direction == GpioDirection.Input)
            {
                EnableEdgeDetection();
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
            // Read in the gpio active_low value
            //
            string activeLowValue = File.ReadAllText(GetActiveLowPath());

            // Decode the output
            //
            switch (activeLowValue.Trim())
            {
                case "0":
                    return false;
                case "1":
                    return true;
                default:
                    throw new InvalidOperationException($"active_low returned unexpected result: {activeLowValue}");
            }
        }

        private void SetActiveLowInternal(bool value)
        {
            try
            {
                File.WriteAllText(GetActiveLowPath(), (value ? "1" : "0"));
            }
            catch { }
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
            // Read in the gpio value
            //
            string pinValue = File.ReadAllText(GetValuePath());

            // Decode the output
            //
            switch (pinValue.Trim())
            {
                case "0":
                    return false;
                case "1":
                    return true;
                default:
                    throw new InvalidOperationException($"value returned unexpected result: {pinValue}");
            }
        }

        private void SetValueInternal(bool value)
        {
            if (Direction == GpioDirection.Output)
            {
                // "/sys/class/gpio/gpio32/value"
                //
                File.WriteAllText(GetValuePath(), (value ? "1" : "0"));
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

        /// <summary>
        /// If true, the pin will be exported when Opened.
        /// Setting this after the pin is opened has no effect.
        /// </summary>
        public bool Export { get; set; }

        /// <summary>
        /// If true, the pin will be unexported when Disposed.
        /// </summary>
        public bool Unexport { get; set; }

        /// <summary>
        /// If true, the pin will use inotifywait to detect pin value changes.
        /// If false, the pin will use polling to detect value changes.
        /// </summary>
        public bool UseInotify { get; set; }

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
                if (pinChangeRegistrationScopes.Count == 0)
                {
                    // Start up the monitoring task
                    //
                    pinMonitorTaskCancellationSource = new CancellationTokenSource();
                    pinMonitorTask = Task.Run(() => PinChangeMonitorTask(pinMonitorTaskCancellationSource.Token), pinMonitorTaskCancellationSource.Token);
                }

                PinChangeRegistrationScope newScope = new PinChangeRegistrationScope(this);
                pinChangeRegistrationScopes.Add(newScope);
                return newScope;
            }
        }

        private void DeregisterPinChangeScope(PinChangeRegistrationScope scope)
        {
            lock (pinChangeRegistrationScopesLock)
            {
                if (pinChangeRegistrationScopes.Remove(scope))
                {
                    if (pinChangeRegistrationScopes.Count == 0)
                    {
                        // Stop the monitoring task
                        //
                        pinMonitorTaskCancellationSource.Cancel();
                        try
                        {
                            pinMonitorTask.GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {

                        }
                        pinMonitorTask = null;
                        pinMonitorTaskCancellationSource = null;
                    }
                }
            }
        }

        private class PinChangeRegistrationScope : IDisposable
        {
            private readonly LinuxGpioPin pin;
            private readonly AwaitableQueue<bool> changesQueue = new AwaitableQueue<bool>();

            public PinChangeRegistrationScope(LinuxGpioPin pin)
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

        private async Task PinChangeMonitorTask(CancellationToken token)
        {
            if (UseInotify)
            {
                await Task.Run(async () => await NotifyWaitHelpers.NotifyWait(
                    GetValuePath(),
                    new string[] { "modify" },
                    (_) => { NotifyAllScopesOfPinChange(this.Value); },
                    token),
                    token);
            }
            else
            {
                bool? previousValue = Value;
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    bool currentValue = Value;

                    if (currentValue != previousValue)
                    {
                        NotifyAllScopesOfPinChange(currentValue);
                    }

                    await Task.Delay(1).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Gets the sysfs based directory for GPIOs.
        /// Usually /sys/class/gpio
        /// </summary>
        /// <returns></returns>
        public static string GetGpioDirectory()
        {
            return gpioDirectory;
        }

        /// <summary>
        /// Gets the sysfs based directory for the specified GPIO pin.
        /// Usually /sys/class/gpio/gpioXX
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        public string GetPinDirectory()
        {
            return Path.Combine(gpioDirectory, "gpio" + Pin.ToString());
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

            if (Unexport)
            {
                TryDisablePin();
            }
        }

        private bool EnableEdgeDetection()
        {
            try
            {
                File.WriteAllText(GetEdgePath(), "both");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the pin is enabled, and if it isn't, enables the pin.
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private bool TryEnablePin()
        {
            if (IsPinExported())
            {
                return true;
            }
            else
            {
                return TryExportPin();
            }
        }

        /// <summary>
        /// Checks if the pin is disabled, and if it isn't, disables the pin.
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private bool TryDisablePin()
        {
            if (IsPinExported())
            {
                return TryUnexportPin();
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Requests the underlying Linux operating system to export
        /// the pin using the GPIO class interface
        /// </summary>
        /// <param name="pin">The pin to export, may fail if it is already exported</param>
        /// <param name="output">Whether this pin should be configured as an output or an input</param>
        /// <returns>Returns true if the pin was enabled successfully</returns>
        private bool TryExportPin()
        {
            // "/sys/class/gpio/export"
            //
            string exporterPath = Path.Combine(gpioDirectory, "export");

            try
            {
                // Export the gpio
                //
                File.WriteAllText(exporterPath, Pin.ToString());
            }
            catch
            {
                //
                // This can happen if the pin already exists.
                // We don't care about this error so much.
                //
            }

            // Kludge:
            // Wait 500ms for the serial port driver to create the directory and for the
            // correct permissions to be set on the files.
            //
            // This shouldn't be necessary, but it's possible that there's a race condition in the
            // GPIO driver.
            //
            Thread.Sleep(500);

            // Verify that the directory now exists
            return IsPinExported();
        }

        /// <summary>
        /// Requests the underlying Linux operating system to unexport
        /// the pin using the GPIO class interface
        /// </summary>
        /// <param name="pin">The pin to unexport</param>
        /// <returns>Returns true if the operation was successful. Will fail if the pin
        /// was already unexported.</returns>
        private bool TryUnexportPin()
        {
            // "/sys/class/gpio/unexport"
            //
            string exporterPath = Path.Combine(gpioDirectory, "unexport");
            try
            {
                // Unexport the gpio
                //
                File.WriteAllText(exporterPath, Pin.ToString());
            }
            catch
            {
                //
                // This can happen if the pin was already unexported.
                // We don't care about this error so much.
                //
            }

            // Kludge:
            // Wait 500ms for the serial port driver to uncreate the directory.
            //
            // This shouldn't be necessary, but it's possible that there's a race condition in the
            // GPIO driver.
            //
            Thread.Sleep(500);

            // Verify that the directory doesn't exist
            return !IsPinExported();
        }

        private bool IsPinExported()
        {
            return Directory.Exists(GetPinDirectory());
        }

        private void EnsurePinIsExported()
        {
            if (!IsPinExported())
            {
                throw new DirectoryNotFoundException($"The GPIO interface directory was not found at {GetPinDirectory()}");
            }
        }

        private string GetValuePath()
        {
            return Path.Combine(GetPinDirectory(), "value");
        }

        private string GetDirectionPath()
        {
            return Path.Combine(GetPinDirectory(), "direction");
        }

        private string GetEdgePath()
        {
            return Path.Combine(GetPinDirectory(), "edge");
        }

        private string GetActiveLowPath()
        {
            return Path.Combine(GetPinDirectory(), "active_low");
        }

        public static bool PlatformSupportsInotify()
        {
            return File.Exists(NotifyWaitHelpers.INotifyWaitPath);
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
