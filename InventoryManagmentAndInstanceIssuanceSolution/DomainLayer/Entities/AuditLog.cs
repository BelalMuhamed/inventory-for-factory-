// DomainLayer/Entities/AuditLog.cs
using System;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Immutable audit record (ERD §5.1). Never updated or deleted. Written by the SaveChanges
    /// interceptor for CRUD and by service hooks for non-CRUD actions (e.g. Login).
    /// </summary>
    public sealed class AuditLog
    {
        /// <summary>Primary key.</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant, or <c>null</c> for system-admin global actions.</summary>
        public long? TenantId { get; set; }

        /// <summary>Actor's tenant id when the actor is a tenant; <c>null</c> for system admins.</summary>
        public long? ActorTenantId { get; set; }

        /// <summary>Actor username (tenant or system admin) — always captured.</summary>
        public string ActorUsername { get; set; } = string.Empty;

        /// <summary>Action: Created/Updated/Deleted/HardDeleted/Login/Upload/Transfer/…</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Name of the affected entity type.</summary>
        public string EntityName { get; set; } = string.Empty;

        /// <summary>Affected entity key as string (composite-key flexible).</summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>Pre-change state as JSON, or <c>null</c>.</summary>
        public string? OldValue { get; set; }

        /// <summary>Post-change state as JSON, or <c>null</c>.</summary>
        public string? NewValue { get; set; }

        /// <summary>Caller IP (IPv4/again IPv6-safe length), or <c>null</c>.</summary>
        public string? IpAddress { get; set; }

        /// <summary>UTC instant of the action.</summary>
        public DateTime Timestamp { get; set; }
    }
}