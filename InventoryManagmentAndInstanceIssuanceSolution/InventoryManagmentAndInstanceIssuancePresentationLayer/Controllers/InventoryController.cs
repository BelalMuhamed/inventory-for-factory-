using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.DTOs.Batches;
using ApplicationLayer.Resources.Localization;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Batch card-upload endpoint (API Spec §4.8, Batch Upload Phased Plan Phase 7). Requires
    /// authentication; the uploading tenant is resolved from the caller's token by
    /// <see cref="IBatchUploadService"/> itself, not by this controller.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs. <see cref="Upload"/> can additionally return this code
    /// with the standard envelope (<c>Batch.ActorNotResolved</c>) when the caller has no
    /// resolvable tenant context — e.g. a system-admin token, which this endpoint doesn't support.
    /// </response>
    [ApiController]
    [Route("api/inventory")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class InventoryController : ControllerBase
    {
        private readonly IServiceManager _services;
        private readonly IStringLocalizer<Messages> _localizer;

        public InventoryController(IServiceManager services, IStringLocalizer<Messages> localizer)
        {
            _services = services;
            _localizer = localizer;
        }

        /// <summary>
        /// Uploads an encrypted batch file of cards. Valid rows are imported (or update an
        /// existing card on re-sight); invalid rows never fail the whole upload — they are
        /// collected and returned as a failed-rows Excel report alongside the counts.
        /// </summary>
        /// <response code="200">
        /// The upload completed — importedCount/failedCount always sum to the file's row count.
        /// Invalid rows never fail the whole upload; failureReportBase64 carries a failed-rows
        /// Excel report (masked PANs only) when failedCount is greater than zero, and is null otherwise.
        /// </response>
        /// <response code="409">A file with this exact fingerprint (FileMac) was already uploaded for this tenant — no rows are written.</response>
        /// <response code="422">The declared ExpectedRowCount doesn't match the file's actual row count, the file isn't a .dat file, is empty, or couldn't be decrypted (wrong key or tampered/corrupted ciphertext).</response>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<BatchUploadResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Upload(
            [FromForm] BatchUploadRequest request,
            CancellationToken cancellationToken)
        {
            FailedRowsReportLabels reportLabels = BuildReportLabels();

            return (await _services.BatchUpload.UploadAsync(request, reportLabels, cancellationToken))
                .ToActionResult(this);
        }

        // Resolves the labels here because IStringLocalizer<Messages> can only be injected in
        // Presentation — Messages physically lives in this project regardless of its namespace.
        // See the XML doc on FailedRowsReportLabels for the full reasoning.
        private FailedRowsReportLabels BuildReportLabels()
        {
            var reasonText = new Dictionary<FailureReason, string>
            {
                [FailureReason.MalformedLine] = _localizer["Batch.FailureReason.MalformedLine"],
                [FailureReason.InvalidPan] = _localizer["Batch.FailureReason.InvalidPan"],
                [FailureReason.DuplicatePanInFile] = _localizer["Batch.FailureReason.DuplicatePanInFile"],
                [FailureReason.UnknownProduct] = _localizer["Batch.FailureReason.UnknownProduct"],
                [FailureReason.UnknownBranch] = _localizer["Batch.FailureReason.UnknownBranch"],
                [FailureReason.CardInTransit] = _localizer["Batch.FailureReason.CardInTransit"],
                [FailureReason.CardDisposed] = _localizer["Batch.FailureReason.CardDisposed"],
            };

            return new FailedRowsReportLabels(
                _localizer["Batch.Report.ColumnMaskedPan"],
                _localizer["Batch.Report.ColumnFailureReason"],
                reasonText);
        }
    }
}
