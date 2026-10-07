using System;

namespace invetoryBackGroundServices.Common
{
    /// <summary>
    /// Payload for machine operations that succeed without returning data of their own (Restore,
    /// EjectCard, and similar). Exists so those endpoints still return a typed
    /// <see cref="ApiResponse{T}"/> body rather than an empty or ad hoc one - the previous
    /// versions returned an anonymous object carrying the machine's raw text.
    /// </summary>
    /// <param name="Operation">Name of the command that completed, e.g. "EjectCard".</param>
    /// <param name="CompletedAt">UTC completion time.</param>
    public sealed record MachineOperationResult(string Operation, DateTime CompletedAt)
    {
        /// <summary>Creates a result stamped at the current UTC time.</summary>
        public MachineOperationResult(string operation) : this(operation, DateTime.UtcNow) { }
    }
}
