using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace invetoryBackGroundServices.Machine.Abstractions
{
    /// <summary>
    /// Vendor-neutral seam between the card job orchestrator and a specific printer family's SDK
    /// (Evolis Integration Plan §4.3/§8/§10). Keyed by <c>UsingPrinterType</c> in DI, one
    /// implementation per vendor — <c>EvolisPrinterAdapter</c> is the only one so far. No
    /// <c>Evolis.*</c> (or any other vendor SDK) type ever crosses this boundary — only
    /// <see cref="PrinterSnapshot"/>, <see cref="CardJob"/>, <see cref="CardJobResult"/>,
    /// <see cref="ICardPrinterConnection"/>, and <c>MachineException</c>.
    /// <para>
    /// Phase 5 design note (see CLAUDE.md): the plan's own file list named this interface as the
    /// place to add <c>ReadTrack2Async</c>/<c>PrintAsync</c>/<c>EjectAsync</c>/<c>RejectAsync</c>,
    /// and originally also declared a single opaque <c>RunJobAsync(CardJob)</c> (Phase 3's stub).
    /// Both can't be true at once: the orchestrator must call the Inventory API
    /// (resolve-for-print) <em>between</em> the magnetic read and the print step, which an opaque
    /// one-shot <c>RunJobAsync</c> can't accommodate without the adapter itself knowing about
    /// <c>IPrintFlowClient</c> — a layering violation this boundary exists to prevent.
    /// <c>RunJobAsync</c> is removed; the four granular operations live on the connection object
    /// <see cref="OpenAsync"/> returns instead, so the reservation is held for the operations'
    /// natural scope (one physical job) without the adapter itself holding any job-shaped state.
    /// </para>
    /// </summary>
    public interface ICardPrinterAdapter
    {
        /// <summary>Lists every printer of this vendor the current machine can see right now.</summary>
        Task<IReadOnlyList<PrinterSnapshot>> DiscoverAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets one printer's current status by its SDK device name, or null if not found.</summary>
        Task<PrinterSnapshot?> GetSnapshotAsync(string deviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens and reserves one physical connection to the named printer for the duration of a
        /// single print job (Evolis Integration Plan §10, step 3), applying <paramref name="job"/>'s
        /// tray and magnetic-coercivity configuration immediately (the plan's own reference table:
        /// coercivity is set "even for read-only use, because it governs how the head interprets
        /// the stripe" — i.e. before step 4's read, not just before step 8's print). The caller
        /// must dispose the returned <see cref="ICardPrinterConnection"/> exactly once — typically
        /// in a <c>finally</c> spanning the whole job (step 12) — which releases the reservation
        /// and closes the connection regardless of how the job ended.
        /// </summary>
        Task<ICardPrinterConnection> OpenAsync(CardJob job, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// One physical job's held connection to a vendor printer (Evolis Integration Plan §10, steps
    /// 3-10) — vendor-neutral, so <c>EvolisCardJobOrchestrator</c> can interleave a real backend
    /// round-trip (resolve-for-print) between the magnetic read and the print step without any
    /// <c>Evolis.*</c> type ever crossing this boundary. Must be disposed exactly once.
    /// </summary>
    public interface ICardPrinterConnection : IAsyncDisposable
    {
        /// <summary>
        /// True once the underlying reservation actually succeeded (best-effort, per
        /// <c>EvolisConnectionFactory</c>'s existing policy — a failed reservation is logged, not
        /// thrown, since a connection can still be usable without one on some real hardware; see
        /// CLAUDE.md's Phase 3 section). The orchestrator's ready gate (step 2) runs before this,
        /// so this is informational for diagnostics, not a precondition the orchestrator re-checks.
        /// </summary>
        bool IsReserved { get; }

        /// <summary>
        /// Reads track 2 only (Evolis Integration Plan §10, step 4, decision Q-24). Per the SDK's
        /// own documentation this inserts a card from the input tray if none is already present —
        /// no separate "load card" step is required, unlike Matica.
        /// </summary>
        Task<MagReadResult> ReadTrack2Async(CancellationToken cancellationToken = default);

        /// <summary>
        /// The ribbon currently loaded in the printer, as the exact <c>Evolis.RibbonType</c> enum
        /// member name (e.g. <c>"KWHITE"</c>) — the same vendor-neutral-once-stringified shape as
        /// <c>RibbonTypes.SdkCode</c>, used by the ribbon compatibility check (step 6). Null when
        /// it could not be read.
        /// </summary>
        Task<string?> GetLoadedRibbonSdkCodeAsync(CancellationToken cancellationToken = default);

        /// <summary>Runs the print operation itself (step 8). Does not eject or reject — the caller decides, per step 10.</summary>
        Task<CardPrintOutcome> PrintAsync(CardPrintRequest request, CancellationToken cancellationToken = default);

        /// <summary>Ejects the card to the configured output tray (step 10, success path).</summary>
        Task<bool> EjectAsync(CancellationToken cancellationToken = default);

        /// <summary>Rejects the card to the configured error tray (step 10, failure path).</summary>
        Task<bool> RejectAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Outcome of <see cref="ICardPrinterConnection.ReadTrack2Async"/>.</summary>
    public enum MagReadOutcome
    {
        /// <summary><see cref="MagReadResult.Track2Data"/> holds the real data read off the stripe.</summary>
        Success,

        /// <summary>The card has no magnetic data at all (SDK: <c>MAG_EBLANK</c>).</summary>
        Blank,

        /// <summary>The card has magnetic data but it could not be read (SDK: <c>MAG_EDATA</c>).</summary>
        Unreadable,

        /// <summary>Any other read failure (mechanical, communication, timeout).</summary>
        Failed
    }

    /// <summary>Result of a track-2-only magnetic read.</summary>
    /// <param name="Outcome">Which of the three cases above occurred.</param>
    /// <param name="Track2Data">The raw data read, only non-null when <paramref name="Outcome"/> is <see cref="MagReadOutcome.Success"/>. Never logged by any caller — see Evolis Integration Plan §10 step 5's raw-PAN handling.</param>
    /// <param name="FailureDetail">Human-readable detail for <see cref="MagReadOutcome.Failed"/>, for logging only.</param>
    public sealed record MagReadResult(MagReadOutcome Outcome, string? Track2Data, string? FailureDetail = null);

    /// <summary>
    /// Everything <see cref="ICardPrinterConnection.PrintAsync"/> needs to run one print operation
    /// — built by <c>EvolisPrintSettingsMapper</c> from a <see cref="CardJob"/> and a
    /// <see cref="Rendering.CardRenderResult"/>. Trays and coercivity are not here — they're
    /// applied once up front by <see cref="ICardPrinterAdapter.OpenAsync"/>, not re-applied per print.
    /// </summary>
    public sealed record CardPrintRequest(
        byte[] BmpBytes,
        Rendering.CardPanel Panel,
        Enums.PrintedFace PrintedFace,
        string RibbonSettingsToken);

    /// <summary>Outcome of one print operation (step 8) — before eject/reject.</summary>
    /// <param name="Succeeded">True when the SDK reported a successful print.</param>
    /// <param name="FailureDetail">Human-readable detail when <paramref name="Succeeded"/> is false, for logging only.</param>
    public sealed record CardPrintOutcome(bool Succeeded, string? FailureDetail = null);
}
