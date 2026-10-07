using DomainLayer.Common;
using DomainLayer.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A single batch-upload run (ERD §3.2). Owned by a bank/tenant (<see cref="BankId"/>) and
    /// recorded against the uploading tenant (<see cref="UploadedByTenantId"/>) — both are the
    /// same tenant today (single-account-per-tenant model) but are kept distinct per the ERD.
    /// <see cref="FileMac"/> is the SHA-256 fingerprint of the decrypted file, used for the
    /// duplicate-file guard (§4.8). Soft-deletable via <see cref="AuditableEntity"/>.
    /// </summary>
    public class Batch : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Owning bank/tenant id (FK → Tenants.Id).</summary>
        [ForeignKey(nameof(Bank))]
        public long BankId { get; set; }

        /// <summary>Navigation to the owning bank/tenant.</summary>
        public Tenant Bank { get; set; } = null!;

        /// <summary>UTC instant the file was uploaded.</summary>
        public DateTime UploadedTime { get; set; }

        /// <summary>Logical batch name supplied by the caller.</summary>
        public string Name { get; set; } = null!;

        /// <summary>Expected row count for validation against <see cref="ProcessedRowCount"/>.</summary>
        public int BatchCardAmount { get; set; }

        /// <summary>
        /// SHA-256 fingerprint of the decrypted file content. Unique per tenant (filtered on
        /// non-deleted rows) — the duplicate-file guard for §4.8.
        /// </summary>
        public string FileMac { get; set; } = null!;

        /// <summary>Original uploaded file name, kept for diagnostics/audit (ERD §3.2).</summary>
        public string OriginalFileName { get; set; } = null!;

        /// <summary>
        /// Outcome of the upload. No default is meaningful here — the orchestrator (Phase 6)
        /// always determines the final outcome before constructing the row — but the property
        /// fail-safes to <see cref="UploadStatus.Failed"/> rather than silently implying success.
        /// </summary>
        public UploadStatus BatchStatus { get; set; } = UploadStatus.Failed;

        /// <summary>Count of rows actually processed (imported + updated), default 0.</summary>
        public int ProcessedRowCount { get; set; }

        /// <summary>
        /// Business-friendly, localized processing error/summary for a failed or partial batch,
        /// or <c>null</c> while pending/succeeded (ERD §3.2). Never a stack trace — unexpected
        /// exceptions are logged via Serilog and surfaced here only as an opaque message.
        /// </summary>
        public string? ProcessingError { get; set; }

        /// <summary>Tenant that performed the upload (FK → Tenants.Id).</summary>
        [ForeignKey(nameof(Tenant))]
        public long UploadedByTenantId { get; set; }

        /// <summary>Navigation to the uploading tenant.</summary>
        public Tenant Tenant { get; set; } = null!;

        /// <summary>Items introduced by this batch. A batch may outlive its items' association (§3.3).</summary>
        public List<ProductItem>? CardsInBatch { get; set; } = new List<ProductItem>();
    }
}
