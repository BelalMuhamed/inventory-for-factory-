namespace invetoryBackGroundServices.Machine.Abstractions
{
    /// <summary>
    /// Coarse operational state of a printer, derived from vendor-specific status flags by each
    /// adapter's own mapper (e.g. <c>EvolisStatusMapper</c>) — never a raw vendor flag itself, so
    /// <see cref="ICardPrinterAdapter"/>'s caller never needs to know which vendor it's talking to.
    /// </summary>
    public enum PrinterState
    {
        /// <summary>State could not be determined.</summary>
        Unknown,

        /// <summary>No error, warning, or blocking condition — can accept a job.</summary>
        Ready,

        /// <summary>Operable, but something needs attention soon (e.g. ribbon running low).</summary>
        Warning,

        /// <summary>Not operable (e.g. cover open, ribbon out, mechanical fault).</summary>
        Error,

        /// <summary>Not reachable at all.</summary>
        Offline
    }

    /// <summary>Hardware capabilities relevant to this plan's print flow, vendor-neutral.</summary>
    public sealed record PrinterCapabilities(bool Magnetic, bool Flip, bool Smart, bool Contactless, bool Ethernet);

    /// <summary>
    /// Vendor-neutral projection of one printer's current status (Evolis Integration Plan §4.3) —
    /// the only shape <see cref="ICardPrinterAdapter"/> exposes. No <c>Evolis.*</c> type crosses
    /// out of <c>Machine/Evolis/</c> into this record; a future Matica adapter populates the same
    /// shape from its own status source.
    /// </summary>
    public sealed record PrinterSnapshot(
        string DeviceName,
        string? DisplayName,
        string? Model,
        string? Uri,
        string? Link,
        bool IsOnline,
        bool IsSupervised,
        bool? SupervisedByEps,
        string? DriverVersion,
        PrinterCapabilities? Capabilities,
        string? RibbonSdkCode,
        PrinterState State);
}
