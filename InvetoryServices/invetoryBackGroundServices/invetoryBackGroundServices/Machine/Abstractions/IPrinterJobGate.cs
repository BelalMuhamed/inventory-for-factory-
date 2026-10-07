using System;
using System.Threading;
using System.Threading.Tasks;

namespace invetoryBackGroundServices.Machine.Abstractions
{
    /// <summary>
    /// Serializes print jobs per physical printer (Evolis Integration Plan §10, "Concurrency").
    /// Not a queue: a second concurrent request for the same <c>deviceName</c> fails fast with
    /// <c>EvolisErrors.PrinterBusy</c> rather than waiting, since the SDK already enforces
    /// single-use at the device via session management and <c>Reserve()</c> — this exists only to
    /// turn that into a clear error instead of a raw SDK busy code. Different devices never block
    /// each other.
    /// </summary>
    public interface IPrinterJobGate
    {
        /// <summary>
        /// Attempts to immediately acquire the gate for <paramref name="deviceName"/>. Returns
        /// null without waiting if another job already holds it — never queues. The returned
        /// handle must be disposed exactly once to release the gate.
        /// </summary>
        IDisposable? TryAcquire(string deviceName);
    }
}
