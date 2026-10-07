using DomainLayer.Common;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A tenant of the platform. Per the ERD (§1.1) and API Spec (§2.1), the tenant record is
    /// itself the authentication identity: there is no separate users table. Exactly one
    /// account exists per tenant, identified by <see cref="Username"/> and verified against
    /// <see cref="PasswordHash"/>.
    /// </summary>
    public class Tenant : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }



        /// <summary>Unique, URL-safe slug. Unique across all tenants.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Whether the tenant is active. A disabled tenant cannot authenticate.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Login identity. Per the ERD this equals the tenant name and is unique
        /// (filtered on <c>IsDeleted = 0</c>).
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Hashed login secret (PBKDF2 via the configured password hasher). The plaintext
        /// password is never stored or logged.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;
    }
}
