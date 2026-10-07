using System;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.PrintFlow;
using invetoryBackGroundServices.Services;
using MATICA_S3300e.LAN;

namespace invetoryBackGroundServices.Outbox
{
    /// <summary>
    /// Reconciles pending <see cref="OutboxEntry"/> records - print outcomes whose Backend Call #2
    /// confirmation to the Inventory API could not be delivered even after the in-request retry
    /// loop was exhausted. Run both on a recurring schedule (every 30 minutes, see
    /// <c>Program.cs</c>) and once at startup, so a crash between the original attempt and the
    /// next scheduled tick doesn't leave anything stranded until the following tick.
    /// <para>
    /// Matica Print Flow, reconciliation-credential phase: this used to reuse each entry's stored
    /// Print Agent token from the <em>original</em> print attempt - a 5-minute-lived token that
    /// was essentially always expired by the time a 30-minute scheduled sweep ran. Fixed by
    /// minting one fresh <see cref="IReconciliationTokenProvider"/> token at the start of every
    /// run and using it for every entry in that run, rather than anything stored per-entry. This
    /// also makes the startup scan just as reliable as the scheduled sweep - both now mint fresh,
    /// so neither depends any more on how much time has passed since the original print attempt.
    /// </para>
    /// </summary>
    public sealed class OutboxReconciliationJob
    {
        private readonly IOutboxStore _store;
        private readonly IPrintFlowClient _printFlowClient;
        private readonly IReconciliationTokenProvider _tokenProvider;
        private readonly Logger _log;

        /// <summary>Creates the job with its collaborators.</summary>
        public OutboxReconciliationJob(
            IOutboxStore store, IPrintFlowClient printFlowClient, IReconciliationTokenProvider tokenProvider, Logger log)
        {
            _store = store;
            _printFlowClient = printFlowClient;
            _tokenProvider = tokenProvider;
            _log = log;
        }

        /// <summary>Attempts to reconcile every pending entry once.</summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _store.GetAllAsync();
            if (entries.Count == 0)
            {
                return;
            }

            PrintFlowResult<string> tokenResult = await _tokenProvider.GetTokenAsync(cancellationToken);
            if (!tokenResult.Success)
            {
                // Can't proceed at all without a token - nothing to reconcile against. Every
                // entry stays queued exactly as it was; the next scheduled run tries again.
                _log.AppendLog(
                    $"Outbox sweep skipped: could not obtain a reconciliation token ({tokenResult.ErrorCode}): " +
                    tokenResult.ErrorMessage, Logger.LogType.Error);
                return;
            }

            _log.AppendLog($"Outbox sweep: {entries.Count} pending entr{(entries.Count == 1 ? "y" : "ies")}.", Logger.LogType.Info);

            string token = tokenResult.Data!;
            bool refreshedThisRun = false;

            foreach (OutboxEntry entry in entries)
            {
                (token, refreshedThisRun) =
                    await ReconcileOneAsync(entry, token, refreshedThisRun, cancellationToken);
            }
        }

        /// <summary>
        /// Reconciles one entry with <paramref name="token"/>. If the call fails in a way that
        /// looks like the token itself is the problem and this run hasn't already refreshed once,
        /// mints a new token and retries this same entry a single time - bounded, not a loop, so a
        /// persistently invalid credential doesn't turn one run into an unbounded retry storm.
        /// </summary>
        private async Task<(string Token, bool RefreshedThisRun)> ReconcileOneAsync(
            OutboxEntry entry, string token, bool refreshedThisRun, CancellationToken cancellationToken)
        {
            PrintFlowResult<object?> result = await _printFlowClient.RecordPrintResultAsync(
                token, entry.ProductItemId, entry.BranchId, entry.Success, entry.HolderName,
                entry.IdempotencyKey, cancellationToken);

            if (!result.Success && !refreshedThisRun && LooksLikeStaleToken(result))
            {
                _log.AppendLog(
                    $"print-result for item {entry.ProductItemId} failed with what looks like a stale " +
                    "token mid-run; minting one fresh token and retrying this entry once.",
                    Logger.LogType.Error);

                PrintFlowResult<string> refreshed = await _tokenProvider.GetTokenAsync(cancellationToken);
                if (refreshed.Success)
                {
                    token = refreshed.Data!;
                    refreshedThisRun = true;
                    result = await _printFlowClient.RecordPrintResultAsync(
                        token, entry.ProductItemId, entry.BranchId, entry.Success, entry.HolderName,
                        entry.IdempotencyKey, cancellationToken);
                }
            }

            if (result.Success)
            {
                await _store.DeleteAsync(entry.IdempotencyKey);
                _log.AppendLog(
                    $"Outbox: reconciled item {entry.ProductItemId}, idempotencyKey {entry.IdempotencyKey}.",
                    Logger.LogType.Info);
                return (token, refreshedThisRun);
            }

            // Still not confirmed - recorded on the entry itself (attempt count, last failure) so
            // the outbox directory doubles as an audit trail for manual reconciliation, then left
            // in place for the next sweep rather than discarded.
            entry.AttemptCount++;
            entry.LastAttemptUtc = DateTime.UtcNow;
            entry.LastFailureReason = result.ErrorMessage;
            await _store.UpdateAsync(entry);

            _log.AppendLog(
                $"Outbox: reconciliation attempt {entry.AttemptCount} for item {entry.ProductItemId}, " +
                $"idempotencyKey {entry.IdempotencyKey} still failing: {result.ErrorMessage}",
                Logger.LogType.Error);

            return (token, refreshedThisRun);
        }

        /// <summary>
        /// A business-category rejection whose error code is <c>Machine.BackendRejected</c> with
        /// an <c>Auth.*</c>-shaped inner reason is what a request authenticated with an
        /// already-expired token looks like from this client's side - the Inventory API rejects it
        /// as an authentication failure, which the print-flow client's own retry logic (transient
        /// vs. business) does not itself distinguish from an ordinary business rejection. This is
        /// a heuristic, not a guarantee - see the remarks on <see cref="ReconcileOneAsync"/> for
        /// why it's bounded to one refresh attempt regardless.
        /// </summary>
        private static bool LooksLikeStaleToken(PrintFlowResult<object?> result) =>
            !string.IsNullOrEmpty(result.ErrorMessage) &&
            (result.ErrorMessage.Contains("401", StringComparison.Ordinal) ||
             result.ErrorMessage.Contains("Unauthenticated", StringComparison.OrdinalIgnoreCase) ||
             result.ErrorMessage.Contains("token", StringComparison.OrdinalIgnoreCase));
    }
}
