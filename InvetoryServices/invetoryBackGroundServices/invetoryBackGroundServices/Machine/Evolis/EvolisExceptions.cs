using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Machine;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Thrown by the Evolis adapter for any SDK failure that should end the current request —
    /// carries the already-mapped <see cref="MachineError"/> (via <see cref="EvolisStatusMapper"/>)
    /// so <see cref="Middleware.GlobalExceptionMiddleware"/> can report it without knowing
    /// anything about the Evolis SDK. Mirrors <see cref="MachineException"/>'s role for the
    /// pre-existing Matica command layer exactly.
    /// </summary>
    public sealed class EvolisException : MachineException
    {
        /// <summary>Creates the exception from an already-mapped error.</summary>
        public EvolisException(MachineError error, string message, System.Exception? inner = null)
            : base(error, message, inner) { }
    }
}
