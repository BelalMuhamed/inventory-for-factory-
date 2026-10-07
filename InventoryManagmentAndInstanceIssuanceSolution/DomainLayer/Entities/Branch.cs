using DomainLayer.Common;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A tenant branch (ERD §2.1). Owned by exactly one <see cref="Tenant"/> via
    /// <see cref="TenantId"/>; soft-deletable through the inherited audit fields.
    /// </summary>
    public sealed class Branch : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        public long TenantId { get; set; }

        /// <summary>Branch display name; unique per tenant among non-deleted rows.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional free-text address (structured fields deferred per ERD §2.1).</summary>
        public string? Location { get; set; }

        /// <summary>Whether the branch is active. Defaults to <c>true</c>.</summary>
        public bool IsActive { get; set; } = true;
    }
}