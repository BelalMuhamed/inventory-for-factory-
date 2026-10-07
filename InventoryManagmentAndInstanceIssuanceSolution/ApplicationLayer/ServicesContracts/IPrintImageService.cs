using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Payload for <c>POST /api/print-images</c> (multipart/form-data). System-admin only —
    /// <paramref name="TenantId"/> is required because the admin caller has no tenant of their
    /// own to infer one from, mirroring <c>CreatePrinterRequest.TenantId</c>.
    /// </summary>
    /// <param name="File">The image to upload.</param>
    /// <param name="TenantId">The tenant the image is being uploaded for.</param>
    public sealed record UploadPrintImageRequest(IFormFile File, long? TenantId);

    /// <summary>
    /// Payload for <c>PUT /api/print-images/{id}</c> — explicit, admin-only replace of an
    /// existing image's content, in place, keeping its id.
    /// </summary>
    /// <param name="File">The replacement image.</param>
    public sealed record ReplacePrintImageRequest(IFormFile File);

    /// <summary>
    /// Resolved content for <c>GET /api/print-images/{id}</c>, handed to
    /// <c>ControllerBase.PhysicalFile</c> to stream the response — not JSON-serialized, so this
    /// type never appears inside an <c>ApiResponse&lt;T&gt;</c> envelope.
    /// </summary>
    /// <param name="PhysicalPath">Absolute on-disk path to the file.</param>
    /// <param name="ContentType">MIME type to send as the response's Content-Type.</param>
    /// <param name="FileName">Original file name, used for the response's Content-Disposition.</param>
    public sealed record PrintImageContent(string PhysicalPath, string ContentType, string FileName);

    /// <summary>
    /// Print-image management use case: upload, retrieve, and explicit replace (revision,
    /// "Print Images &amp; Product Print Configuration" change request). Upload and replace are
    /// system-admin only — reversed from the original design, where only tenants could upload.
    /// Retrieval is open to both roles, scoped to the caller's own tenant for a tenant caller.
    /// </summary>
    public interface IPrintImageService
    {
        /// <summary>
        /// Validates and saves the uploaded image under the target tenant's folder (named after
        /// their sanitized username, not their numeric id), using the sanitized original file
        /// name as the physical name. Create-only: when a non-deleted image with the same
        /// original file name already exists for that tenant, nothing is saved —
        /// <see cref="UploadPrintImageResult.Created"/> is <c>false</c> and
        /// <see cref="UploadPrintImageResult.Image"/> is the <em>existing</em> image's metadata,
        /// so the caller can choose to keep it as-is or call <see cref="ReplaceAsync"/>
        /// explicitly. This is a genuine outcome, not a failure — the result is
        /// success either way; only the controller inspects
        /// <c>Created</c> to choose between <c>200 OK</c> and <c>409 Conflict</c>.
        /// </summary>
        Task<Result<UploadPrintImageResult>> UploadAsync(
            UploadPrintImageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Explicitly replaces an existing image's content, in place — same
        /// <c>PrintImage.Id</c>, new bytes (and new name/content-type/size if the replacement
        /// file differs from the original). Because the id never changes, any product print
        /// configuration already referencing it by <c>ImageId</c> is unaffected. Rejects with
        /// <c>PrintingErrors.PrintImageNameConflict</c> if the replacement's file name collides
        /// with a <em>different</em> existing image for the same tenant.
        /// </summary>
        Task<Result<PrintImageResponse>> ReplaceAsync(
            long id, ReplacePrintImageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the physical content for <c>GET /api/print-images/{id}</c>. A tenant caller
        /// may only retrieve images belonging to their own tenant; a mismatch returns
        /// <c>PrintingErrors.PrintImageNotFound</c>, the same as a genuinely missing id (no
        /// existence leak). A system admin may retrieve any image.
        /// </summary>
        Task<Result<PrintImageContent>> GetContentAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// One-time, admin-triggered, idempotent migration of images uploaded under the original
        /// scheme (GUID physical name, tenant-id folder) to the current scheme (sanitized original
        /// name, tenant-username folder). Safe to run more than once — a row already matching the
        /// current scheme is left alone. Not a scheduled job; the locked decision against a
        /// background cleanup mechanism still holds — this is an explicit, on-demand operational
        /// tool, run when the admin chooses to run it.
        /// </summary>
        Task<Result<MigrateLegacyImagesResult>> MigrateLegacyImagesAsync(CancellationToken cancellationToken = default);
    }
}
