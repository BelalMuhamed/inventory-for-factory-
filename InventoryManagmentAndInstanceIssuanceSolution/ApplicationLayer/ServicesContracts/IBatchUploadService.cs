using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.DTOs.Batches;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// The batch-upload use case (Batch Upload Phased Plan, Phase 6): decrypt → validate →
    /// upsert <c>ProductItem</c>s → recompute <c>Stock</c> → record the <c>Batch</c>, all in one
    /// DB transaction, plus a failed-rows report when needed.
    /// </summary>
    public interface IBatchUploadService
    {
        /// <summary>
        /// Runs the full batch-upload pipeline for one uploaded file. The uploading tenant is
        /// resolved internally from <c>ICurrentTenant</c> (matching every other service in this
        /// codebase — see <c>ProductService</c>/<c>BranchService</c>/<c>StockService</c>) rather
        /// than being passed in by the caller.
        /// </summary>
        /// <param name="request">The uploaded file, batch name, and declared row count.</param>
        /// <param name="reportLabels">
        /// Already-localized column headers and failure-reason text for the failed-rows report.
        /// Resolved by the caller (Presentation), which is the only layer that can see the
        /// <c>Messages</c> localization resource — see the XML doc on
        /// <see cref="FailedRowsReportLabels"/> for why.
        /// </param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>
        /// <see cref="Result{TValue}.Failure"/> for whole-file issues (empty file, row-count
        /// mismatch, duplicate file, decryption failure), an unresolvable caller (system-admin
        /// tokens have no tenant to upload for), or a genuinely unexpected exception. Row-level
        /// failures are a collected outcome, not a failure — a file where every row failed
        /// validation still returns <see cref="Result{TValue}.Success"/> with
        /// <c>ImportedCount == 0</c> and the failed-rows report attached.
        /// </returns>
        Task<Result<BatchUploadResult>> UploadAsync(
            BatchUploadRequest request,
            FailedRowsReportLabels reportLabels,
            CancellationToken cancellationToken = default);
    }
}
