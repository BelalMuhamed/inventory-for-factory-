namespace DomainLayer.Enums
{
    /// <summary>
    /// Outcome of a batch-upload run (ERD §3.2 / Batch Upload Phased Plan). The pipeline runs
    /// synchronously inside one request/transaction (Phase 6) — there is no queued/async state,
    /// so no Pending or Processing value exists. The <see cref="Batch"/> row is written only
    /// once the outcome is already known.
    /// </summary>
    public enum UploadStatus
    {
        /// <summary>Every row in the file was imported or upserted successfully.</summary>
        Succeeded = 0,

        /// <summary>At least one row succeeded and at least one row failed (see the failed-rows report).</summary>
        PartialSuccess = 1,

        /// <summary>The whole file was rejected (e.g. decryption failure, duplicate file) or every row failed.</summary>
        Failed = 2
    }
}
