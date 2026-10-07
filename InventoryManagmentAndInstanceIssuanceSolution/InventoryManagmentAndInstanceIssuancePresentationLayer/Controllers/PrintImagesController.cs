using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Print-image endpoints (revision, "Print Images &amp; Product Print Configuration" change
    /// request). Upload, replace, and migration are system-admin only — reversed from the
    /// original design, where only tenants could upload. Retrieval is open to both roles, scoped
    /// to the caller's own tenant for a tenant caller. Every image is served through
    /// <see cref="Get"/>; there is no public static-file path for this content any more.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs.
    /// </response>
    /// <response code="403">
    /// A tenant caller called <see cref="Upload"/>, <see cref="Replace"/>, or
    /// <see cref="MigrateLegacyStorage"/> (all system-admin only). Enforced in the service and
    /// returned as the standard <see cref="ApiResponse{T}"/> envelope
    /// (<c>PrintImage.OnlySystemAdmin</c>), not an authorization-policy rejection.
    /// </response>
    [ApiController]
    [Route("api/print-images")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class PrintImagesController : ControllerBase
    {
        private readonly IServiceManager _services;

        public PrintImagesController(IServiceManager services) => _services = services;

        /// <summary>
        /// Uploads a print image for a tenant (system-admin only; <c>TenantId</c> is required in
        /// the form data). Create-only: if a non-deleted image with the same original file name
        /// already exists for that tenant, nothing is saved and the response is
        /// <c>409 Conflict</c> carrying the <em>existing</em> image's metadata, so the caller can
        /// choose to keep it as-is or call <see cref="Replace"/> explicitly.
        /// </summary>
        /// <response code="200">A new image was saved.</response>
        /// <response code="409">
        /// Create-only: a non-deleted image with this exact original file name already exists for
        /// the target tenant, so nothing was saved. The body is a normal success envelope carrying
        /// the EXISTING image's metadata (<see cref="PrintImageResponse"/>), not an ApiError —
        /// this is the one 409 in this API that isn't a failure envelope.
        /// </response>
        /// <response code="403">A tenant caller attempted this — system-admin only.</response>
        /// <response code="422">No file was supplied, the file is too large, its extension isn't allowed, or its actual content doesn't match a supported image format.</response>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<PrintImageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PrintImageResponse>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Upload(
            [FromForm] UploadPrintImageRequest request, CancellationToken cancellationToken)
        {
            Result<UploadPrintImageResult> result = await _services.PrintImages.UploadAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToActionResult(this);
            }

            UploadPrintImageResult upload = result.Value;
            if (!upload.Created)
            {
                // Not an Error — ToActionResult's status mapping is driven entirely by
                // ErrorCategory and only ever applies to a failed Result, so a genuinely
                // successful-but-not-created outcome (the existing image's own metadata) is
                // built directly here rather than forcing it through that path.
                var body = ApiResponse<PrintImageResponse>.Ok(upload.Image, HttpContext.TraceIdentifier);
                return new ObjectResult(body) { StatusCode = StatusCodes.Status409Conflict };
            }

            return Result.Success(upload.Image).ToActionResult(this);
        }

        /// <summary>
        /// Explicitly replaces an existing image's content, in place (system-admin only) — same
        /// id, new bytes. Any product print configuration already referencing this
        /// <c>ImageId</c> is unaffected, since the id never changes.
        /// </summary>
        /// <response code="200">The image's content was replaced in place.</response>
        /// <response code="403">A tenant caller attempted this — system-admin only.</response>
        /// <response code="404">No print image exists with the supplied id.</response>
        /// <response code="409">The replacement's file name collides with a different, existing image for the same tenant (a genuine ApiError, unlike Upload's 409).</response>
        /// <response code="422">No file was supplied, the file is too large, its extension isn't allowed, or its actual content doesn't match a supported image format.</response>
        [HttpPut("{id:long}")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<PrintImageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Replace(
            long id, [FromForm] ReplacePrintImageRequest request, CancellationToken cancellationToken)
            => (await _services.PrintImages.ReplaceAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Streams a print image's bytes. Both roles may call this; a tenant caller may only
        /// retrieve images belonging to their own tenant. This is the only way to fetch an
        /// image's content — there is no public static-file URL for it any more.
        /// </summary>
        /// <response code="200">The raw image bytes (image/png or image/jpeg) — not a JSON envelope.</response>
        /// <response code="404">No print image exists with the supplied id, or it belongs to another tenant.</response>
        [HttpGet("{id:long}")]
        [Produces("image/png", "image/jpeg")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
        {
            Result<PrintImageContent> result = await _services.PrintImages.GetContentAsync(id, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToActionResult(this);
            }

            PrintImageContent content = result.Value;
            return PhysicalFile(content.PhysicalPath, content.ContentType, content.FileName);
        }

        /// <summary>
        /// One-time, admin-triggered, idempotent migration of images uploaded under the original
        /// scheme (GUID physical name, tenant-id folder) to the current scheme (sanitized original
        /// name, tenant-username folder). Safe to run more than once — already-migrated rows are
        /// left alone.
        /// </summary>
        /// <response code="200">Migration run summary. Safe to call more than once.</response>
        /// <response code="403">A tenant caller attempted this — system-admin only.</response>
        [HttpPost("migrate-legacy-storage")]
        [ProducesResponseType(typeof(ApiResponse<MigrateLegacyImagesResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> MigrateLegacyStorage(CancellationToken cancellationToken)
            => (await _services.PrintImages.MigrateLegacyImagesAsync(cancellationToken)).ToActionResult(this);
    }
}
