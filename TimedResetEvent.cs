using System;
using System.Threading;
using System.Timers;

// Avoid ambiguity between threading.timer and timers.timer
using Timer = System.Timers.Timer;

namespace crozone.LinuxGpio
{
    public class TimedAutoResetEvent : IDisposable
    {
        public TimedAutoResetEvent(bool set)
        {
            innerResetEvent = new AutoResetEvent(set);

            resetTimer = new Timer()
            {
                AutoReset = false,
                Enabled = false
            };

            resetTimer.Elapsed += ResetTimer_Elapsed;
        }

        private AutoResetEvent innerResetEvent;
        private readonly Timer resetTimer;
        private readonly object timerLock = new object();
        private volatile bool isDisposed = false;

        private void ResetTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isDisposed)
            {
                return;
            }

            // Set the event upon the timer elapsing.
            //
            innerResetEvent.Set();
        }

        public void WaitOne()
        {
            innerResetEvent.WaitOne();
        }

        /// <summary>
        /// Starts a timer that will set the event after a given amount of time.
        /// If the timer is already running, it is reset, pushing the reset time forward.
        /// </summary>
        public void SetAfter(TimeSpan timespan)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsyncTimedResetEvent));
            }

            lock (timerLock)
            {
                // Stop the timer
                //
                resetTimer.Enabled = false;

                // Set the new interval
                //
                resetTimer.Interval = timespan.TotalMilliseconds;

                // Start the timer again
                //
                resetTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Resets the event, and starts a timer that will set the event after a given amount of time.
        /// If the timer is already running, it is reset, pushing the reset time forward.
        /// </summary>
        public void ResetAndSetAfter(TimeSpan timespan)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsyncTimedResetEvent));
            }

            lock (timerLock)
            {
                // Stop the timer and reset the event.
                //
                ResetAndStop();

                // Set the new interval
                //
                resetTimer.Interval = timespan.TotalMilliseconds;

                // Start the timer again
                //
                resetTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Stops any currently running timers, and sets the event.
        /// </summary>
        public void SetAndStop()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsyncTimedResetEvent));
            }

            lock (timerLock)
            {
                // Stop current timer
                //
                resetTimer.Enabled = false;

                // Set event
                //
                innerResetEvent.Set();
            }
        }

        /// <summary>
        /// Stops any currently running timers, and resets the event.
        /// </summary>
        public void ResetAndStop()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(AsyncTimedResetEvent));
            }

            lock (timerLock)
            {
                // Stop current timer
                //
                resetTimer.Enabled = false;

                // Reset event
                //
                innerResetEvent.Reset();
            }
        }

        public void Dispose()
        {
            isDisposed = true;

            lock (timerLock)
            {
                resetTimer.Dispose();
            }
        }
    }
}
