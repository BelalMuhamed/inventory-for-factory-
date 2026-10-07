using System;
using System.Collections.Concurrent;
using System.Threading;
using invetoryBackGroundServices.Machine.Abstractions;

namespace invetoryBackGroundServices.Machine.Orchestration
{
    /// <summary>
    /// <see cref="IPrinterJobGate"/> implementation: one <see cref="SemaphoreSlim"/>(1,1) per
    /// device name, created lazily and kept for the process lifetime (Evolis Integration Plan
    /// §10, "Concurrency"). Registered as a singleton — the dictionary itself is the shared state
    /// across every request.
    /// </summary>
    public sealed class PrinterJobGate : IPrinterJobGate
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

        /// <inheritdoc />
        public IDisposable? TryAcquire(string deviceName)
        {
            SemaphoreSlim semaphore = _semaphores.GetOrAdd(deviceName, static _ => new SemaphoreSlim(1, 1));
            return semaphore.Wait(0) ? new Releaser(semaphore) : null;
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _released;

            public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _semaphore.Release();
            }
        }
    }
}
