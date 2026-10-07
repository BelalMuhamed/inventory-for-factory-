using System;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Metadata for an uploaded print-configuration image (module requirements §5/§6/§7).
    /// <see cref="Id"/> is the single source of truth other entities reference by — see
    /// <c>MaticaProductPrintConfiguration.ImageId</c> / <c>EvolisProductPrintConfiguration.ImageId</c>.
    /// Not soft-deletable and does not derive from <see cref="Common.AuditableEntity"/>.
    /// <para>
    /// <b>Revised design:</b> uploading no longer auto-replaces a same-name image (the original
    /// decision Q-10 behavior) — a duplicate name is reported back to the client instead
    /// (<c>PrintImageService.UploadAsync</c> returns <c>Created = false</c> with the existing
    /// row's data), and replacing it is now a separate, explicit action
    /// (<c>PUT /api/print-images/{id}</c>) that updates this same row's content in place, keeping
    /// <see cref="Id"/> constant — so a product print configuration referencing it by id is never
    /// left dangling by a replace.
    /// </para>
    /// <para>
    /// <b>Scope note:</b> the locked decisions explicitly rule out a scheduled cleanup mechanism
    /// for orphaned uploads (no <c>BackgroundService</c>, no <c>SystemCurrentTenant</c>). This
    /// entity therefore carries no "Pending/Attached" lifecycle state — there is no consumer for
    /// one. An image that is uploaded but never referenced by a product's print configuration
    /// stays on disk; that trade-off is accepted, not solved, by this module (flagged for
    /// awareness — the same kind of documented, deliberate omission this codebase already accepts
    /// elsewhere, e.g. <c>BatchUploadService</c>'s "no mark-Failed-on-exception" note).
    /// </para>
    /// </summary>
    public sealed class PrintImage
    {
        /// <summary>Primary key (BIGINT IDENTITY). The stable identifier other entities reference.</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Every physical path is scoped under this tenant's folder.</summary>
        public long TenantId { get; set; }

        /// <summary>
        /// The file name exactly as supplied by the uploading client (only a directory component,
        /// if any, is stripped — never sanitized against invalid characters). Kept faithful to
        /// what the client sent so duplicate detection matches the client's own notion of the
        /// file's name; the filesystem-safe version used physically on disk is
        /// <see cref="StoredFileName"/>, which can differ (e.g. an accented or punctuated name).
        /// Unique per tenant: uploading a second file with the same name does not overwrite this
        /// row — it is reported back as a conflict instead (see the class doc comment).
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// Sanitized, filesystem-safe physical file name on disk — derived from
        /// <see cref="OriginalFileName"/> but not always identical to it.
        /// </summary>
        public string StoredFileName { get; set; } = string.Empty;

        /// <summary>
        /// Path relative to the configured image root, e.g. <c>acme-corp/student-card-front.png</c>
        /// for a tenant whose username sanitizes to <c>acme-corp</c> — the tenant's folder is named
        /// after their username, not their numeric id. Physical layout only; clients never see or
        /// construct this directly — they retrieve the image via <c>GET /api/print-images/{id}</c>.
        /// </summary>
        public string StoredPath { get; set; } = string.Empty;

        /// <summary>
        /// MIME type detected from the file's magic bytes at upload time — never the
        /// client-supplied Content-Type header, which is not trusted.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>UTC instant the file was uploaded (or last replaced via <c>PUT /api/print-images/{id}</c>).</summary>
        public DateTime UploadedAt { get; set; }
    }
}
