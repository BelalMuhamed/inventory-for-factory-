using DomainLayer.Common;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Bootstrap system-administrator account that lives outside the tenant model
    /// (API Spec §2.4). It manages tenants only and never performs regular tenant operations.
    /// A token issued for this account carries <c>isSystemAdmin = true</c> and bypasses all
    /// tenant query filters.
    /// <para>
    /// <b>Schema note:</b> the ERD allows either a standalone table or fixed configuration for
    /// this account. This codebase implements the table option (approved) so the credential can
    /// be rotated and its logins audited.
    /// </para>
    /// </summary>
    public class SystemAdmin : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Unique login identity for the administrator.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Hashed login secret (PBKDF2). Plaintext is never stored or logged.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Whether the administrator account is active and may authenticate.</summary>
        public bool IsActive { get; set; } = true;
    }
}
