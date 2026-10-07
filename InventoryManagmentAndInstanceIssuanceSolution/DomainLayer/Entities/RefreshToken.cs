using System;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A persisted, rotatable refresh token backing the <c>/api/auth/refresh</c> and
    /// <c>/api/auth/logout</c> endpoints (API Spec §4.1). Only a hash of the token value is
    /// stored; the raw token is returned to the client once and never persisted.
    /// <para>
    /// A token belongs to exactly one principal, identified by <see cref="userName"/>.
    /// <see cref="IsSystemAdmin"/> records whether that principal is the bootstrap system admin
    /// (<c>true</c>) or a tenant (<c>false</c>), captured at issue time so rotation re-issues a
    /// token of the same kind. On refresh the current token is revoked and a successor is issued,
    /// with <see cref="ReplacedByTokenHash"/> linking the two for audit and reuse-detection.
    /// </para>
    /// <para>
    /// <b>Schema note:</b> not present in the original ERD; added to support refresh/logout
    /// (approved). Flagged for DBA review.
    /// </para>
    /// </summary>
    public class RefreshToken
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Login username of the principal this token was issued to (tenant or system admin).</summary>
        public string userName { get; set; }

        /// <summary>
        /// True when this token was issued to the bootstrap system admin. Captured at issue time so
        /// rotation re-mints an admin access token rather than silently downgrading to a tenant token.
        /// </summary>
        public bool IsSystemAdmin { get; set; }

        /// <summary>Hash of the opaque refresh-token value. The raw value is never stored.</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>UTC instant the token was issued.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC instant the token expires and can no longer be exchanged.</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>UTC instant the token was revoked (via rotation or logout), or <c>null</c> if still live.</summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>Hash of the token that superseded this one on rotation, or <c>null</c>.</summary>
        public string? ReplacedByTokenHash { get; set; }

        /// <summary>True when the token has neither been revoked nor expired at <paramref name="utcNow"/>.</summary>
        /// <param name="utcNow">The current UTC instant to evaluate against.</param>
        public bool IsActive(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;
        /// <summary>
        /// Owning tenant's id when this token was issued to a tenant; <c>null</c> for a system-admin token.
        /// Captured at issue time so rotation can re-mint the <c>tenantId</c> claim without a DB lookup.
        /// </summary>
        public long? TenantId { get; set; }
    }
}