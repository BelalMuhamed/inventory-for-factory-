using DomainLayer.Common;
using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A product (card type / SKU) in a tenant's catalog (ERD §2.2). Owned by exactly one
    /// <see cref="Tenant"/> via <see cref="TenantId"/>; soft-deletable through the inherited audit
    /// fields. Its name is unique per tenant among non-deleted rows.
    /// </summary>
    public sealed class Product : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        public long TenantId { get; set; }

        /// <summary>Product display name; unique per tenant among non-deleted rows.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the product is active or inactive (ERD §8). Defaults to Active.</summary>
        public ActivationStatus ActivationStatus { get; set; } = ActivationStatus.Active;

        /// <summary>
        /// Reorder threshold: available stock at or below this value flags the product as low.
        /// Defaults to 0. Stock comparison is applied once the Stock aggregate exists (ERD §3.1).
        /// </summary>
        public int LowProductThreshold { get; set; }

        /// <summary>How the product's items are tracked across transactions (ERD §8).</summary>
         
        public ProductTransactionWay ProductTransactionWay { get; set; }

        /// <summary>Printer family used to print this product (ERD §8).</summary>
        public UsingPrinterType UsingPrinterType { get; set; }
    }
}
