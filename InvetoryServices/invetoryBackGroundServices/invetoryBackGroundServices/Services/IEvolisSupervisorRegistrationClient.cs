using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.PrintFlow;

namespace invetoryBackGroundServices.Services
{
    /// <summary>One device reported alongside a supervisor-registration call — matches the Inventory API's <c>DiscoveredDeviceDto</c>.</summary>
    public sealed class DiscoveredDeviceDto
    {
        public string DeviceName { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Link { get; set; }
        public string? DriverVersion { get; set; }
        public bool IsSupervised { get; set; }
    }

    /// <summary>Request body for <c>POST api/printers/supervisor-registration</c> — matches the Inventory API's <c>SupervisorRegistrationRequest</c>.</summary>
    public sealed class SupervisorRegistrationRequestDto
    {
        public long BranchId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public IReadOnlyList<DiscoveredDeviceDto> Devices { get; set; } = System.Array.Empty<DiscoveredDeviceDto>();
    }

    /// <summary>One partition entry — matches the Inventory API's <c>MatchedSupervisorDevice</c>.</summary>
    public sealed class MatchedSupervisorDeviceDto
    {
        public long PrinterId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public bool Updated { get; set; }
    }

    /// <summary>One partition entry — matches the Inventory API's <c>UnmatchedDiscoveredDevice</c>.</summary>
    public sealed class UnmatchedDiscoveredDeviceDto
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>One partition entry — matches the Inventory API's <c>UnseenRegisteredPrinter</c>.</summary>
    public sealed class UnseenRegisteredPrinterDto
    {
        public long PrinterId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
    }

    /// <summary>Response body — matches the Inventory API's <c>SupervisorRegistrationResponse</c>.</summary>
    public sealed class SupervisorRegistrationResultDto
    {
        public IReadOnlyList<MatchedSupervisorDeviceDto> Matched { get; set; } = System.Array.Empty<MatchedSupervisorDeviceDto>();
        public IReadOnlyList<UnmatchedDiscoveredDeviceDto> Unmatched { get; set; } = System.Array.Empty<UnmatchedDiscoveredDeviceDto>();
        public IReadOnlyList<UnseenRegisteredPrinterDto> RegisteredButNotDiscovered { get; set; } = System.Array.Empty<UnseenRegisteredPrinterDto>();
    }

    /// <summary>
    /// Pushes this machine's own Evolis discovery results to the Inventory API's
    /// supervisor-registration endpoint (Evolis Integration Plan §7/§8, decisions Q-21/Q-28).
    /// </summary>
    public interface IEvolisSupervisorRegistrationClient
    {
        /// <summary>
        /// Registers this host as the supervisor of every matching device in
        /// <paramref name="devices"/>, under the reconciliation service account's access token.
        /// </summary>
        Task<PrintFlowResult<SupervisorRegistrationResultDto>> RegisterAsync(
            string accessToken,
            long branchId,
            string hostName,
            IReadOnlyList<DiscoveredDeviceDto> devices,
            CancellationToken cancellationToken);
    }
}
