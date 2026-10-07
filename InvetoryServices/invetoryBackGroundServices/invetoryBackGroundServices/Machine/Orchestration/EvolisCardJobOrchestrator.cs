using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Enums;
using invetoryBackGroundServices.Machine.Abstractions;
using invetoryBackGroundServices.Machine.Evolis;
using invetoryBackGroundServices.Machine.Rendering;
using invetoryBackGroundServices.Outbox;
using invetoryBackGroundServices.PrintFlow;
using invetoryBackGroundServices.Services;
using MATICA_S3300e.CLS;
using MATICA_S3300e.LAN;
using Microsoft.Extensions.DependencyInjection;

namespace invetoryBackGroundServices.Machine.Orchestration
{
    /// <summary>
    /// <see cref="IEvolisCardJobOrchestrator"/> implementation — the twelve steps of Evolis
    /// Integration Plan §10, run in the order explained below.
    /// <para>
    /// Execution order deliberately differs from the plan's own step numbering in one place (see
    /// CLAUDE.md's Phase 5 section for the full reasoning): rendering runs <em>first</em>, before
    /// the printer gate, the ready check, or any card is touched — not as step 7, between the
    /// ribbon check (step 6) and the print (step 8). Two reasons: the ribbon check's own rule
    /// reads <c>renderResult.Panel</c>, which can't exist before rendering runs; and rendering is
    /// a pure, hardware-free function (Phase 4), so running it first means an invalid cardholder
    /// name (<see cref="Rendering.InvalidCardHolderNameException"/>) is rejected with zero side
    /// effects — no card pulled from the feeder, no resolve-for-print call made — instead of
    /// discovering it after a card and a backend round-trip have already been spent.
    /// </para>
    /// <para>
    /// Every early failure (steps 0, 2, 4, 5, 6) throws <see cref="EvolisException"/> after doing
    /// any necessary card disposition first — mirroring <c>MachineController.Print</c>'s own
    /// catch-eject-rethrow shape for Matica exactly, just as a thrown exception instead of an
    /// early <c>return</c>, since this orchestrator has no <see cref="Microsoft.AspNetCore.Mvc.IActionResult"/>
    /// to return directly. <see cref="Middleware.GlobalExceptionMiddleware"/> turns it into the
    /// right HTTP status the same way it already does for every other <c>MachineException</c>.
    /// Only steps 8-11's outcome (print succeeded, or failed after disposition and Backend Call #2
    /// already ran) is returned as a <see cref="CardJobResult"/> rather than thrown — matching
    /// Matica's own <c>PrintResult</c>/207 shape exactly, including its one preserved quirk: an
    /// eject failure after a successful print still reports <c>CannotEject</c> even though
    /// Backend Call #2 already ran with <c>success: true</c> by that point.
    /// </para>
    /// </summary>
    public sealed class EvolisCardJobOrchestrator : IEvolisCardJobOrchestrator
    {
        private readonly ICardPrinterAdapter _adapter;
        private readonly ICardRenderer _renderer;
        private readonly IPanExtractor _panExtractor;
        private readonly EvolisRibbonCompatibilityChecker _ribbonChecker;
        private readonly EvolisPrintSettingsMapper _settingsMapper;
        private readonly IPrinterJobGate _gate;
        private readonly IPrintFlowClient _printFlowClient;
        private readonly IOutboxStore _outbox;
        private readonly Logger _log;

        public EvolisCardJobOrchestrator(
            [FromKeyedServices(UsingPrinterType.Evolis)] ICardPrinterAdapter adapter,
            ICardRenderer renderer,
            IPanExtractor panExtractor,
            EvolisRibbonCompatibilityChecker ribbonChecker,
            EvolisPrintSettingsMapper settingsMapper,
            IPrinterJobGate gate,
            IPrintFlowClient printFlowClient,
            IOutboxStore outbox,
            Logger log)
        {
            _adapter = adapter;
            _renderer = renderer;
            _panExtractor = panExtractor;
            _ribbonChecker = ribbonChecker;
            _settingsMapper = settingsMapper;
            _gate = gate;
            _printFlowClient = printFlowClient;
            _outbox = outbox;
            _log = log;
        }

        /// <inheritdoc />
        public async Task<CardJobResult> RunAsync(
            CardJob job, string tenantId, string bearerToken, CancellationToken cancellationToken)
        {
            // Step 0 (see class remarks for why this runs first): render before touching anything
            // physical. Lets InvalidCardHolderNameException propagate as-is below - the controller
            // maps it, nothing here needs to react to it specially.
            CardRenderResult renderResult;
            try
            {
                renderResult = _renderer.Render(new CardRenderRequest(
                    job.CardHolderName, job.PrintWay, job.X, job.Y, job.PrintedFace,
                    job.FontFamily, job.FontSize, job.PrintColor, job.BackgroundColor, job.FontStyle));
            }
            catch (InvalidCardHolderNameException ex)
            {
                throw new EvolisException(EvolisErrors.InvalidCardHolderName(ex.Message), ex.Message, ex);
            }

            foreach (string warning in renderResult.Warnings)
            {
                _log.AppendLog($"[Product:{job.ProductId}] render warning: {warning}", Logger.LogType.Info);
            }

            // Step 1: per-device gate - fails fast, never queues.
            using IDisposable? gateHandle = _gate.TryAcquire(job.DeviceName);
            if (gateHandle is null)
            {
                throw new EvolisException(EvolisErrors.PrinterBusy(), "This Evolis printer is already busy with another print job.");
            }

            // Step 2: ready gate.
            PrinterSnapshot? snapshot = await _adapter.GetSnapshotAsync(job.DeviceName, cancellationToken);
            MachineError? notReady = CheckReady(snapshot);
            if (notReady is not null)
            {
                throw new EvolisException(notReady.Value, notReady.Value.Message);
            }

            // Generated once per physical attempt, reused on every retry of Backend Call #2 - same
            // rule as Matica's own idempotencyKey.
            string idempotencyKey = Guid.NewGuid().ToString();

            // Steps 3 + 12: open+reserve here, dispose in the finally below regardless of outcome.
            ICardPrinterConnection connection = await _adapter.OpenAsync(job, cancellationToken);
            try
            {
                // Step 4: magnetic read (Q-24). Per the SDK's own documentation this inserts a
                // card from the feeder if none is already present - no separate load-card step.
                MagReadResult magResult = await connection.ReadTrack2Async(cancellationToken);
                if (magResult.Outcome != MagReadOutcome.Success)
                {
                    await TryRejectAsync(connection, cancellationToken);
                    MachineError magError = magResult.Outcome == MagReadOutcome.Blank
                        ? EvolisErrors.BlankTrack(magResult.FailureDetail)
                        : EvolisErrors.MagneticError(magResult.FailureDetail);
                    throw new EvolisException(magError, magError.Message);
                }

                // Step 5: PAN -> Backend Call #1. The raw PAN is used only for this call - never
                // logged, never persisted, never assigned anywhere else (Evolis Integration Plan
                // §10 step 5).
                string pan = _panExtractor.Extract(magResult.Track2Data!);
                PrintFlowResult<ResolveForPrintResponseDto> resolveResult = await _printFlowClient.ResolveForPrintAsync(
                    bearerToken, pan, job.ProductId, job.BranchId, cancellationToken);
                // pan is not referenced again.

                if (!resolveResult.Success)
                {
                    await TryRejectAsync(connection, cancellationToken);
                    _log.AppendLog(
                        $"[Card Holder Name:{job.CardHolderName}] [Product:{job.ProductId}] resolve-for-print failed: {resolveResult.ErrorMessage}",
                        Logger.LogType.Error);

                    MachineError backendError = resolveResult.IsTransient
                        ? MachineErrors.BackendUnavailable(resolveResult.ErrorMessage)
                        : MachineErrors.BackendRejected(resolveResult.ErrorMessage);
                    throw new EvolisException(backendError, backendError.Message);
                }

                long productItemId = resolveResult.Data!.ProductItemId;
                string maskedPan = resolveResult.Data.MaskedPan;

                // Step 6: ribbon compatibility (Q-25) - run after rendering so renderResult.Panel
                // already exists (see class remarks).
                string? loadedRibbonSdkCode = await connection.GetLoadedRibbonSdkCodeAsync(cancellationToken);
                if (loadedRibbonSdkCode is null)
                {
                    await TryRejectAsync(connection, cancellationToken);
                    MachineError error = EvolisErrors.UnknownRibbon("could not read the loaded ribbon");
                    throw new EvolisException(error, error.Message);
                }

                RibbonCompatibilityResult compatibility = _ribbonChecker.Check(
                    loadedRibbonSdkCode, job.RibbonSdkCode, renderResult.Panel);
                if (compatibility.Status != RibbonCompatibility.Ok)
                {
                    await TryRejectAsync(connection, cancellationToken);
                    MachineError ribbonError = compatibility.Status == RibbonCompatibility.Mismatch
                        ? EvolisErrors.RibbonMismatch(compatibility.ExpectedRibbonSdkCode, compatibility.LoadedRibbonSdkCode)
                        : EvolisErrors.RibbonCannotPrintColour(compatibility.LoadedRibbonSdkCode);
                    throw new EvolisException(ribbonError, ribbonError.Message);
                }

                if (!_settingsMapper.TryMapRibbon(job.RibbonSdkCode, out string? ribbonSettingsToken))
                {
                    await TryRejectAsync(connection, cancellationToken);
                    MachineError error = EvolisErrors.RibbonNotSelectable(job.RibbonSdkCode);
                    throw new EvolisException(error, error.Message);
                }

                // Step 8: print. Caught locally, not thrown - the flow must continue to the local
                // batch write and Backend Call #2 regardless of whether the physical print
                // succeeded, same as Matica's Emboss step.
                CardPrintRequest printRequest = _settingsMapper.ToPrintRequest(job, renderResult, ribbonSettingsToken!);
                bool printSucceeded;
                try
                {
                    CardPrintOutcome outcome = await connection.PrintAsync(printRequest, cancellationToken);
                    printSucceeded = outcome.Succeeded;
                    if (!printSucceeded)
                    {
                        _log.AppendLog($"Print failed: {outcome.FailureDetail}", Logger.LogType.Error);
                    }
                }
                catch (Exception ex)
                {
                    printSucceeded = false;
                    _log.AppendLog($"Print threw: {ex.Message}", Logger.LogType.Error);
                }

                // Step 9: encrypted local batch line - same format/cipher as Matica's (Q-27
                // limitation: duplicated by hand, see CLAUDE.md's Phase 5 section).
                WriteLocalBatchLine(maskedPan, job, tenantId, printSucceeded);

                // Step 10: disposition before Backend Call #2 - the physical card is already
                // printed-or-spoiled by this point, so it goes to the operator/reject tray
                // promptly instead of sitting inside the machine during the retry loop.
                bool dispositionSucceeded;
                try
                {
                    dispositionSucceeded = printSucceeded
                        ? await connection.EjectAsync(cancellationToken)
                        : await connection.RejectAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    dispositionSucceeded = false;
                    _log.AppendLog($"{(printSucceeded ? "Eject" : "Reject")} threw: {ex.Message}", Logger.LogType.Error);
                }

                // Step 11: Backend Call #2.
                PrintFlowResult<object?> recordResult = await _printFlowClient.RecordPrintResultAsync(
                    bearerToken, productItemId, job.BranchId, printSucceeded, job.CardHolderName,
                    idempotencyKey, cancellationToken);

                if (printSucceeded && !dispositionSucceeded)
                {
                    // Preserved quirk, matching Matica's MachineController.Print exactly: Backend
                    // Call #2 has already run with success: true by this point, but the eject
                    // failure is still reported as this request's own failure.
                    MachineError error = MachineErrors.CannotEject();
                    throw new EvolisException(error, error.Message);
                }

                if (!printSucceeded)
                {
                    MachineError error = MachineErrors.Rejected("Error printing card.");
                    throw new EvolisException(error, error.Message);
                }

                if (!recordResult.Success)
                {
                    _log.AppendLog(
                        $"print-result could not be confirmed for item {productItemId}, idempotencyKey {idempotencyKey}: {recordResult.ErrorMessage}",
                        Logger.LogType.Error);

                    await _outbox.SaveAsync(new OutboxEntry
                    {
                        IdempotencyKey = idempotencyKey,
                        ProductItemId = productItemId,
                        BranchId = job.BranchId,
                        Success = true,
                        HolderName = job.CardHolderName,
                        CreatedAtUtc = DateTime.UtcNow
                    });

                    return new CardJobResult(true, false, productItemId, idempotencyKey, null);
                }

                return new CardJobResult(true, true, productItemId, idempotencyKey, null);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }

        /// <summary>
        /// Step 2's ready gate. Vendor-neutral <see cref="PrinterSnapshot"/> is coarser than the
        /// plan's own pseudocode (individual <c>WAR_*</c>/<c>INF_CARD_*</c> flags) — confirmed in
        /// Phase 3 that <c>GetStatus</c> itself genuinely fails for some real connection paths
        /// (TCP, on this deployment's own hardware), so <see cref="PrinterState.Unknown"/> and a
        /// null <see cref="PrinterCapabilities"/> are treated as "proceed cautiously," not
        /// "reject" — rejecting them would make every TCP-linked Evolis printer in this
        /// deployment permanently unprintable, the opposite of the plan's own graceful-degradation
        /// rule (§13.4 #7). Only a confirmed bad read (<see cref="PrinterState.Error"/>/
        /// <see cref="PrinterState.Offline"/>/<see cref="PrinterState.Warning"/>) or a confirmed
        /// missing magnetic encoder rejects outright.
        /// </summary>
        private static MachineError? CheckReady(PrinterSnapshot? snapshot)
        {
            if (snapshot is null)
            {
                return MachineErrors.NotReady("printer not found");
            }

            if (snapshot.State is PrinterState.Error or PrinterState.Offline or PrinterState.Warning)
            {
                return MachineErrors.NotReady($"printer state: {snapshot.State}");
            }

            if (snapshot.Capabilities is { Magnetic: false })
            {
                return MachineErrors.NotReady("printer has no magnetic encoder");
            }

            return null;
        }

        private async Task TryRejectAsync(ICardPrinterConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await connection.RejectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _log.AppendLog($"Reject (after an earlier failure) threw: {ex.Message}", Logger.LogType.Error);
            }
        }

        /// <summary>
        /// Mirrors <c>MachineController.Print</c>'s local batch line exactly - same field order,
        /// same <c>ENCRYPTION.Enc_TripleDES</c> cipher, same <c>GLOBALS._KEY_CONFIG</c> key. Q-27
        /// limitation noted in the plan: duplicated by hand rather than shared, since Matica's own
        /// orchestration is still inline in its controller action, not behind this interface.
        /// </summary>
        private void WriteLocalBatchLine(string maskedPan, CardJob job, string tenantId, bool printSucceeded)
        {
            string logData = $" [Card Holder Name:{job.CardHolderName}] [Product:{job.ProductId}] " +
                $"[Card PAN:{maskedPan}] [Status:{(printSucceeded ? "Success" : "Error")}]";
            _log.AppendLog(logData, printSucceeded ? Logger.LogType.Info : Logger.LogType.Error);

            string localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");
            if (!File.Exists(localBatch))
            {
                File.Create(localBatch).Close();
            }

            using var streamWriter = new StreamWriter(localBatch, true);
            string lineValue = $"{maskedPan}|{job.CardHolderName}|{job.BranchId}|{tenantId}|" +
                $"{(printSucceeded ? 1 : 3)}|{(printSucceeded ? "Print Card Success" : "Error in Print Card")}|{job.ProductId}";
            string lineValueCipher = ENCRYPTION.Enc_TripleDES(out string _, lineValue, GLOBALS._KEY_CONFIG);
            streamWriter.WriteLine(lineValueCipher);
        }
    }
}
