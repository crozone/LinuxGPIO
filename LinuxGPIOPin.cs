using crozone.AsyncResetEvents;
using System;
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
    public class LinuxGpioPin : IGpioPin, IDisposable
    {
        private static readonly string gpioDirectory = Path.DirectorySeparatorChar + Path.Combine("sys", "class", "gpio");

        private readonly TimeSpan defaultAssertionTime = TimeSpan.FromMilliseconds(50);
        private readonly TimeSpan defaultDeassertionTime = TimeSpan.FromMilliseconds(50);
        private readonly TimeSpan defaultDebounceTime = TimeSpan.FromMilliseconds(5);

        private const bool useInotify = true;
        private readonly bool export;
        private readonly bool unexport;
        private readonly GpioDirection? initialDirection;
        private readonly bool? initialActiveLow;
        private readonly bool? initialValue;
        private bool? valueCache = default(bool?);
        private bool isDisposed;

        public int Pin { get; private set; }
        public string Name { get; private set; }
        public TimeSpan AssertionTime { get; private set; }
        public TimeSpan DeassertionTime { get; private set; }

        public TimeSpan DebounceTime { get; private set; }

        /// <summary>
        /// Represents a controller for GPIO control on any Linux system with 
        /// GPIO hardware support enabled.
        /// </summary>
        ///
        public LinuxGpioPin(
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
            this.Pin = pin;
            this.Name = name;
            this.export = export;
            this.unexport = unexport;
            this.initialDirection = direction;
            this.initialActiveLow = activeLow;
            this.initialValue = initialValue;
            this.AssertionTime = assertionTime ?? defaultAssertionTime;
            this.DeassertionTime = deassertionTime ?? defaultDeassertionTime;
            this.DebounceTime = debounceTime ?? defaultDebounceTime;

            isDisposed = false;

            if (export)
            {
                EnsurePinEnabled();
            }

            EnsurePinDirectoryExists();

            if (initialActiveLow.HasValue)
            {
                ActiveLow = initialActiveLow.Value;
            }

            valueCache = initialValue;

            if (initialDirection.HasValue)
            {
                Direction = initialDirection.Value;
            }
        }

        /// <summary>
        /// Gets or sets the GPIO pin's direction.
        /// </summary>
        public GpioDirection Direction {
            get {
                ThrowIfDisposed();

                string currentDirection = File.ReadAllText(GetDirectionPath());

                if ("in" == currentDirection)
                {
                    return GpioDirection.Input;
                }
                else if ("out" == currentDirection)
                {
                    return GpioDirection.Output;
                }
                else
                {
                    throw new InvalidOperationException($"Pin direction returned {currentDirection}, which appears to be invalid.");
                }
            }
            set {
                ThrowIfDisposed();

                string directionString = value.ToDirectionString(valueCache);

                // "sys/class/gpio/gpioXX/direction"
                //
                string pinDirectionPath = GetDirectionPath();

                // Set the direction
                //
                File.WriteAllText(pinDirectionPath, directionString);

                // Set edge detection if the direction is input
                //
                if (value == GpioDirection.Input)
                {
                    EnableEdgeDetection();
                }
            }
        }

        public bool ActiveLow {
            get {
                ThrowIfDisposed();

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
            set {
                ThrowIfDisposed();

                try
                {
                    File.WriteAllText(GetActiveLowPath(), (value ? "1" : "0"));
                }
                catch { }
            }
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
            set {
                ThrowIfDisposed();

                valueCache = value;

                try
                {
                    // "/sys/class/gpio/gpio32/value"
                    //
                    File.WriteAllText(GetValuePath(), (value ? "1" : "0"));
                }
                catch { }
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

        public async Task WaitForSteadyState(bool state, CancellationToken token)
        {
            ThrowIfDisposed();

            // Change strategy based on whether inotify is available or not
            //
            if (useInotify && SupportsInotify())
            {
                await WaitForSteadyStateInotify(state, token);
            }
            else
            {
                await WaitForSteadyStatePoll(state, token);
            }
        }

        public override string ToString()
        {
            return $"[{Pin}] {Name}";
        }

        public void Dispose()
        {
            isDisposed = true;

            if (unexport)
            {
                EnsurePinDisabled();
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

        private async Task WaitForSteadyStatePoll(bool state, CancellationToken token)
        {
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();

            while (true)
            {
                token.ThrowIfCancellationRequested();

                await Task.Delay(1);

                if (this.Value = state)
                {
                    if (stopwatch.Elapsed > DebounceTime)
                    {
                        return;
                    }
                }
                else
                {
                    stopwatch.Restart();
                }
            }
        }

        private async Task WaitForSteadyStateInotify(bool state, CancellationToken token)
        {
            // AsyncAutoResetEvent for tracking when the pin changes.
            // When this is Unset, the pin has not changed, and we don't need to repoll its state.
            // When this is Set, the pin has changed, and we need to repoll it.
            //
            AsyncAutoResetEvent pinChangedEvent = new AsyncAutoResetEvent(true);

            // Timed async reset event for tracking the pin stable state.
            // When this is Unset, the pin state is not stable.
            // When this is Set, the pin state is stable.
            //
            AsyncTimedResetEvent pinStableEvent = new AsyncTimedResetEvent(true, true);

            // This is called when a new pin change notification is received.
            // It is passed to the WaitNotify method, which calls it synchronously
            // on its threadpool thread.
            //
            void notifyCallback(NotifyWaitResponse response)
            {
                // Every time the pin changes, reset the reset event to notify waiters that the pin changed
                //
                pinChangedEvent.Set();

                // Reset the stable event to indicate the pin is unstable.
                //
                pinStableEvent.ResetAndSetAfter(DebounceTime);
            }

            // This is the cancellation source for the NotifyWait task.
            // It is fired when we want to stop the NotifyWait task.
            //
            CancellationTokenSource notifyWaitTaskCancellationSource = new CancellationTokenSource();

            // This is the NotifyWait task. It waits on an inotify mechanism
            // and calls the notifyCallback whenever an inotify modify event occurs
            // on the GPIO value file for this pin.
            //
            Task notifyWaitTask = Task.Run(async () => await NotifyWaitHelpers.NotifyWait(
                GetValuePath(),
                new string[] { "modify" },
                notifyCallback,
                notifyWaitTaskCancellationSource.Token),
                notifyWaitTaskCancellationSource.Token);

            try
            {
                // Since we don't know the state of the pin before this method was called,
                // reset the stable event and have it set itself after debounce time.
                //
                pinStableEvent.ResetAndSetAfter(DebounceTime);

                // Loop wait for the pin value to become both steady, and the value we want.
                //
                while (true)
                {
                    // Exit the loop with an OperationCanceledException if the cancellation source
                    // was triggered.
                    //
                    token.ThrowIfCancellationRequested();

                    // Wait for the pin changed event
                    //
                    await pinChangedEvent.WaitAsync(token);

                    // Compare the current state to the pin's current value
                    //
                    if (Value != state)
                    {
                        // If the state isn't the state we want, just go back to waiting now
                        //
                        continue;
                    }

                    //
                    // The state is correct, now we just need to wait and see if it remains stable.
                    //

                    // Wait to be notified that the pin state has remained stable
                    //
                    await pinStableEvent.WaitAsync(token);

                    //
                    // Measure the state again. It should be the same as the last measurement to be considered stable
                    // for this loop.
                    //

                    // Check that the last state is in the state we want.
                    //
                    if (Value != state)
                    {
                        // The state is not the state we want. Go around the loop and wait more.
                        //
                        continue;
                    }

                    //
                    // We have reached the state we were waiting for.
                    //

                    // Break the waiting loop.
                    //
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                // Rethrow the cancellation exception.
                //
                throw;
            }
            finally
            {
                //
                // At this point, we have either successfully waited for
                // the pin to change state and become steady, or the task
                // has been cancelled. Either way, we need to clean up.
                //

                // Trigger the notify wait task cancellation source
                //
                notifyWaitTaskCancellationSource.Cancel();

                // Wait for the notify wait task to cancel
                //
                try
                {
                    await notifyWaitTask;
                }
                catch (OperationCanceledException)
                {

                }

                // Dispose of the cancellation token source
                //
                notifyWaitTaskCancellationSource.Dispose();

                // Dispose the timed reset event
                //
                pinStableEvent.Dispose();
            }
        }

        /// <summary>
        /// Checks if the pin is enabled, and if it isn't, enables the pin.
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private bool EnsurePinEnabled()
        {
            if (IsPinEnabled())
            {
                return true;
            }
            else
            {
                return TryEnablePin();
            }
        }

        /// <summary>
        /// Checks if the pin is disabled, and if it isn't, disables the pin.
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private bool EnsurePinDisabled()
        {
            if (IsPinEnabled())
            {
                return TryDisablePin();
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
        private bool TryEnablePin()
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
            return IsPinEnabled();
        }

        /// <summary>
        /// Requests the underlying Linux operating system to unexport
        /// the pin using the GPIO class interface
        /// </summary>
        /// <param name="pin">The pin to unexport</param>
        /// <returns>Returns true if the operation was successful. Will fail if the pin
        /// was already unexported.</returns>
        private bool TryDisablePin()
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
            return !IsPinEnabled();
        }

        private bool IsPinEnabled()
        {
            return Directory.Exists(GetPinDirectory());
        }

        private void EnsurePinDirectoryExists()
        {
            if (!IsPinEnabled())
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

        private bool SupportsInotify()
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
    }
}
