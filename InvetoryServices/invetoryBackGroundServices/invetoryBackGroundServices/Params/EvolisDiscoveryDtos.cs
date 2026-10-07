using System.Collections.Generic;
using invetoryBackGroundServices.Machine.Abstractions;

namespace invetoryBackGroundServices.Params
{
    /// <summary>Capabilities block of the discovery response (Evolis Integration Plan §8). Null when <c>GetStatus</c> could not be read.</summary>
    public sealed record EvolisCapabilitiesDto(bool Magnetic, bool Flip, bool Smart, bool Contactless, bool Ethernet);

    /// <summary>Currently-loaded-ribbon block of the discovery response. Null when <c>GetRibbonInfo</c> could not be read.</summary>
    public sealed record EvolisRibbonDto(string SdkCode);

    /// <summary>
    /// One Evolis device the Agent's own <c>Evolis.GetDevices()</c> call found on this machine
    /// (Evolis Integration Plan §8, decision Q-22 — a device proxy, not registry data).
    /// <see cref="Capabilities"/>, <see cref="SupervisedByEps"/>, <see cref="Ribbon"/>, and
    /// <see cref="State"/> are all null/<see cref="PrinterState.Unknown"/> when the live status
    /// read they depend on fails — confirmed this can happen independently of the device itself
    /// being online (see <c>CLAUDE.md</c>'s Phase 3 section for the live hardware finding) — per
    /// plan rule §13.4 #7, this degrades gracefully rather than failing the whole discovery call.
    /// </summary>
    public sealed record EvolisDiscoveredPrinterDto(
        string DeviceName,
        string? DisplayName,
        string? Model,
        string? Uri,
        string? Link,
        bool IsOnline,
        bool IsSupervised,
        string? DriverVersion,
        EvolisCapabilitiesDto? Capabilities,
        bool? SupervisedByEps,
        EvolisRibbonDto? Ribbon,
        PrinterState State);

    /// <summary>Response body for <c>GET /api/machine/evolis/printers</c>.</summary>
    public sealed record EvolisDiscoveryResponse(string HostName, IReadOnlyList<EvolisDiscoveredPrinterDto> Devices);
}
