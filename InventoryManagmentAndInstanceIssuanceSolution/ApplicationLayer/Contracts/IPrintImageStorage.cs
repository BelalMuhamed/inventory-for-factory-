using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Common;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Outcome of a successful physical save.
    /// </summary>
    /// <param name="StoredFileName">
    /// Sanitized physical file name, including extension — equal to the sanitized original
    /// client-supplied name (no longer a generated GUID; see the revised upload design).
    /// </param>
    /// <param name="StoredPath">
    /// Path relative to the configured image root, e.g. <c>acme-corp/front.png</c> — physical
    /// layout only. Clients never see or construct this; they retrieve images via
    /// <c>GET /api/print-images/{id}</c>.
    /// </param>
    /// <param name="ContentType">MIME type detected from the file's magic bytes, not the client-supplied header.</param>
    /// <param name="SizeBytes">File size in bytes.</param>
    public sealed record StoredImage(string StoredFileName, string StoredPath, string ContentType, long SizeBytes);

    /// <summary>
    /// Physical storage for uploaded print-configuration images. Pure I/O — no database access,
    /// no duplicate-name policy decisions; that orchestration belongs to <c>IPrintImageService</c>,
    /// which pairs this with <see cref="IPrintImageRepo"/>. Kept as its own abstraction so a
    /// future storage backend (e.g. blob storage) is a single new implementation, matching this
    /// codebase's existing pattern of infrastructure-facing interfaces living in
    /// <c>ApplicationLayer.Contracts</c> (e.g. <c>IBatchFileEncryptor</c>).
    /// </summary>
    public interface IPrintImageStorage
    {
        /// <summary>
        /// Validates and saves <paramref name="file"/> under
        /// <c>{root}/{tenantFolder}/{sanitized-original-name}</c>. The physical file name is the
        /// sanitized original client-supplied name — not a generated identifier — so uploading
        /// the same logical file twice under different metadata rows would collide on disk; the
        /// caller (<c>IPrintImageService</c>) is responsible for the duplicate-name policy
        /// decision before calling this. Validates maximum size, extension allowlist, and the
        /// file's actual content via magic-byte signature sniffing; the client-supplied
        /// <c>Content-Type</c> header is never trusted.
        /// </summary>
        /// <param name="tenantFolder">
        /// Sanitized, filesystem-safe tenant folder name (derived from the tenant's username, not
        /// their numeric id) — determines the physical subdirectory. The caller sanitizes this;
        /// this method trusts it as already safe.
        /// </param>
        /// <param name="file">The uploaded file.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<StoredImage>> SaveAsync(
            string tenantFolder, IFormFile file, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the physical file at <paramref name="storedPath"/>, if present. Never throws
        /// for a missing file — deleting something already gone is not a failure for this
        /// method's caller.
        /// </summary>
        Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves <paramref name="storedPath"/> (relative, as stored on
        /// <c>PrintImage.StoredPath</c>) to an absolute physical path, for the retrieval endpoint
        /// to hand to <c>ControllerBase.PhysicalFile</c>. Does not check the file exists — the
        /// caller is expected to have already confirmed the owning <c>PrintImage</c> row exists
        /// and the caller may access it; this method only resolves the path.
        /// </summary>
        string GetPhysicalPath(string storedPath);

        /// <summary>
        /// Moves an already-saved file from <paramref name="oldRelativePath"/> to
        /// <paramref name="newRelativePath"/> (both relative to the configured root), creating the
        /// destination directory if needed. Used by the one-time legacy-image migration
        /// (renaming GUID-named, tenant-id-foldered files to the current
        /// original-name/tenant-username scheme) — not part of the normal upload/replace flow.
        /// Returns <c>false</c> (does not throw) if the source file does not exist or the
        /// destination already exists, so the caller can report a per-row outcome rather than
        /// aborting a bulk operation.
        /// </summary>
        Task<bool> MoveAsync(
            string oldRelativePath, string newRelativePath, CancellationToken cancellationToken = default);
    }
}
