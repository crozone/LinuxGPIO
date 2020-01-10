using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace crozone.LinuxGpio
{
    public class AwaitableQueue<T> : IDisposable
    {
        private volatile bool isDisposed = false;
        private SemaphoreSlim semaphore = new SemaphoreSlim(0);
        private readonly object queueLock = new object();
        private Queue<T> queue = new Queue<T>();

        public void Enqueue(T item)
        {
            ThrowIfDisposed();

            lock (queueLock)
            {
                queue.Enqueue(item);
                semaphore.Release();
            }
        }

        public T WaitAndDequeue(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            semaphore.Wait(timeSpan, cancellationToken);
            lock (queueLock)
            {
                return queue.Dequeue();
            }
        }

        public async Task<T> WhenDequeue(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await semaphore.WaitAsync(timeSpan, cancellationToken);
            lock (queueLock)
            {
                return queue.Dequeue();
            }
        }

        public void Dispose()
        {
            isDisposed = true;
            semaphore.Dispose();

        }

        /// <summary>
        /// Throw an ObjectDisposedException if the object is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(AwaitableQueue<T>));
        }
    }
}
