using System;
using invetoryBackGroundServices.Common;

namespace invetoryBackGroundServices.Machine
{
    /// <summary>
    /// Base for every failure raised by the machine-communication layer. Carries the
    /// <see cref="MachineError"/> the middleware/controller should report, so the mapping from
    /// "what went wrong" to "what the caller sees" lives in one place instead of being
    /// re-derived from exception types at each call site.
    /// <para>
    /// Note the deliberate behavioral change from the pre-existing synchronous
    /// <c>MachineCommands.CommandManagement</c>, which caught every exception internally and
    /// collapsed it into <c>(reply = -1, sMessage = ex.Message)</c> - meaning nothing ever
    /// propagated far enough for centralized handling to be possible. The new layer throws
    /// instead, which is what makes the exception middleware and the standardized error contract
    /// actually work. The old synchronous path is untouched and still behaves as before.
    /// </para>
    /// </summary>
    public abstract class MachineException : Exception
    {
        /// <summary>The error to report for this failure.</summary>
        public MachineError Error { get; }

        /// <summary>Creates the exception with its reportable error.</summary>
        protected MachineException(MachineError error, string message, Exception? inner = null)
            : base(message, inner) => Error = error;
    }

    /// <summary>Could not reach the machine at all (connection refused, unreachable, TLS failure).</summary>
    public sealed class MachineCommunicationException : MachineException
    {
        /// <summary>Creates the exception.</summary>
        public MachineCommunicationException(string message, Exception? inner = null)
            : base(MachineErrors.ConnectionFailed(message), message, inner) { }
    }

    /// <summary>The machine did not respond within the configured timeout.</summary>
    public sealed class MachineTimeoutException : MachineException
    {
        /// <summary>Creates the exception.</summary>
        public MachineTimeoutException(string message, Exception? inner = null)
            : base(MachineErrors.Timeout(message), message, inner) { }
    }

    /// <summary>The machine responded, but the response could not be parsed as expected.</summary>
    public sealed class MachineProtocolException : MachineException
    {
        /// <summary>Creates the exception.</summary>
        public MachineProtocolException(string message, Exception? inner = null)
            : base(MachineErrors.UnexpectedResponse(message), message, inner) { }
    }

    /// <summary>The machine responded and explicitly reported an error for the command.</summary>
    public sealed class MachineRejectedException : MachineException
    {
        /// <summary>Creates the exception.</summary>
        public MachineRejectedException(string message, Exception? inner = null)
            : base(MachineErrors.Rejected(message), message, inner) { }
    }
}
