namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// Why a batch-upload row failed (Batch Upload Phased Plan, Phase 6 per-row rules). The
    /// first three are produced by <see cref="IBatchRowParser"/> (Phase 4, no DB); the last two
    /// are produced by the orchestrator (Phase 6) after checking the row against the tenant's
    /// product/branch maps — declared here so both phases share one vocabulary.
    /// </summary>
    public enum FailureReason
    {
        /// <summary>Wrong field count, or a required field was empty.</summary>
        MalformedLine,

        /// <summary>Non-numeric, wrong length (not 13–19 digits), or failed the Luhn check.</summary>
        InvalidPan,

        /// <summary>The same PAN appeared earlier in this file; only the first occurrence proceeds.</summary>
        DuplicatePanInFile,

        /// <summary>The row's product name does not match any product in the tenant's catalog.</summary>
        UnknownProduct,

        /// <summary>The row's branch name does not match any branch in the tenant's catalog.</summary>
        UnknownBranch,

        /// <summary>
        /// The card exists but is currently in transit or unassigned (<c>BranchID IS NULL</c>), so
        /// the re-sight was refused (Transactions §4.10, T0). Silently honouring the file's branch
        /// would yank the card out of an in-flight transfer and desynchronize the hold quantities
        /// on both sides; the uploader is told instead, and the rest of the file still processes.
        /// </summary>
        CardInTransit,

        /// <summary>
        /// The card exists but has been disposed (written off) and has permanently left inventory
        /// (Transactions §4.10, Addendum A). A disposed card is never resurrected by a re-upload.
        /// </summary>
        CardDisposed
    }
}
