using ApplicationLayer.BatchUpload;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.DTOs.Batches
{
    /// <summary>
    /// Request payload for <c>POST /api/inventory/upload</c> (API Spec §4.8, multipart/form-data).
    /// </summary>
    /// <param name="File">The encrypted batch file.</param>
    /// <param name="BatchName">Logical batch name (persisted as <c>Batch.Name</c>).</param>
    /// <param name="ExpectedRowCount">
    /// Caller-declared row count, checked against the file's actual row count
    /// (<c>BatchErrors.ExpectedRowCountMismatch</c> on mismatch).
    /// </param>
    public sealed record BatchUploadRequest(IFormFile File, string BatchName, int ExpectedRowCount);

    /// <summary>
    /// Result payload for a completed batch upload (API Spec §4.8). Returned only once the whole
    /// pipeline has finished — there is no partial/pending response (Phase 6 runs synchronously,
    /// one transaction).
    /// </summary>
    /// <param name="ImportedCount">
    /// Rows that became a new <c>ProductItem</c> or updated an existing one on re-sight (both
    /// count as success per §6.4).
    /// </param>
    /// <param name="FailedCount">Rows that did not import, for any reason (see <see cref="FailureReason"/>).</param>
    /// <param name="FailureReportFileName">
    /// Suggested file name for the failed-rows report, or <c>null</c> when <see cref="FailedCount"/> is 0.
    /// </param>
    /// <param name="FailureReportBase64">
    /// Base64-encoded failed-rows Excel report (Phase 5), or <c>null</c> when <see cref="FailedCount"/> is 0.
    /// Never contains a real PAN — masked PAN only.
    /// </param>
    public sealed record BatchUploadResult(
        int ImportedCount,
        int FailedCount,
        string? FailureReportFileName,
        string? FailureReportBase64);
}
