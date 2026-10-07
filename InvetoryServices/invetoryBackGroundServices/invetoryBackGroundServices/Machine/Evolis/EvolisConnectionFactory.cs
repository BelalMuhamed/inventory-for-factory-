using System;
using Evolis;
using invetoryBackGroundServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Opens a fresh <see cref="Connection"/> per operation, configured from
    /// <see cref="EvolisOptions"/> (Evolis Integration Plan §8). Deliberately not a connection
    /// pool: the SDK auto-releases a session after 45s of inactivity regardless, a pooled
    /// connection held across jobs would keep the printer's LCD locked and block a walk-up
    /// operator, and a crashed job must never leak a reservation. Open cost is small relative to
    /// a multi-second print.
    /// </summary>
    public sealed class EvolisConnectionFactory
    {
        private readonly EvolisOptions _options;
        private readonly ILogger<EvolisConnectionFactory> _logger;

        /// <summary>Creates the factory from its bound options.</summary>
        public EvolisConnectionFactory(IOptions<EvolisOptions> options, ILogger<EvolisConnectionFactory> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Opens a connection to the device by its SDK connection name (<c>Evolis.Device.name</c>).
        /// <paramref name="reserve"/> is <c>false</c> for a read-only status/discovery probe (plan
        /// §8: reserving would lock the LCD and disrupt an operator) and <c>true</c> for an actual
        /// print job (Phase 5). Setting error management and reserving are both best-effort here —
        /// confirmed live against real hardware that either can fail independently of whether the
        /// connection itself opened, so a failure is logged, never thrown; the caller decides
        /// whether it can still proceed with degraded information.
        /// </summary>
        public EvolisConnectionScope Open(string deviceName, bool reserve = false)
        {
            OpenMode mode = ParseOpenMode(_options.OpenMode);
            var connection = new Connection(deviceName, mode);

            ErrorManagement errorManagement = ParseErrorManagement(_options.ErrorManagement);
            if (!connection.SetErrorManagement(errorManagement))
            {
                _logger.LogWarning(
                    "SetErrorManagement({Mode}) failed for device '{DeviceName}': {LastError}",
                    errorManagement, deviceName, connection.GetLastError());
            }

            bool reserved = false;
            if (reserve && _options.ReserveOnJob)
            {
                reserved = connection.Reserve(_options.CommandTimeoutMs, _options.CommandTimeoutMs);
                if (!reserved)
                {
                    _logger.LogWarning(
                        "Reserve() failed for device '{DeviceName}': {LastError}",
                        deviceName, connection.GetLastError());
                }
            }

            return new EvolisConnectionScope(connection, reserved);
        }

        private static OpenMode ParseOpenMode(string value) =>
            Enum.TryParse(value, ignoreCase: true, out OpenMode mode) ? mode : OpenMode.DIRECT;

        private static ErrorManagement ParseErrorManagement(string value) =>
            Enum.TryParse(value, ignoreCase: true, out ErrorManagement em) ? em : ErrorManagement.SOFTWARE;
    }
}
