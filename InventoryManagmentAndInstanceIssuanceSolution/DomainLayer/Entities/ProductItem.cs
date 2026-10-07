using DomainLayer.Common;
using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A single physical card instance (ERD §3.3). <see cref="PanFingerprint"/> is the sole
    /// identity/dedup key — a deterministic HMAC-SHA256 fingerprint of the normalized full PAN
    /// (see <c>IPanFingerprintGenerator</c>) — and is unique per tenant among non-deleted rows.
    /// It is never displayed, logged, or returned by any API.
    /// <see cref="MaskedPan"/> is the display/search-safe derivative computed at ingestion time
    /// from the plaintext PAN (never derived from <see cref="PanFingerprint"/> — see
    /// <c>PanMasker</c>): ten mask characters concatenated with the last six PAN digits. It is
    /// used exclusively for display and user-facing search, never for identity or dedup.
    /// <see cref="BatchId"/> is required: an item always belongs to the batch that introduced
    /// it, and deleting that batch deletes its items (Cascade).
    /// </summary>
    public class ProductItem : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long ID { get; set; }

        /// <summary>
        /// Deterministic HMAC-SHA256 fingerprint (32 bytes) of the normalized full PAN. The sole
        /// identity/dedup key for this tenant — drives the unique index and the batch-upload
        /// re-sight lookup. Never displayed, never logged, never returned by any API response.
        /// See <c>IPanFingerprintGenerator</c>.
        /// </summary>
        public byte[] PanFingerprint { get; set; } = null!;

        /// <summary>
        /// Masked PAN persisted at ingestion time: ten mask characters ("**********") followed
        /// by the last six PAN digits. The only field used throughout the application for
        /// display and any user-facing search — never used for identity or duplicate detection.
        /// </summary>
        public string MaskedPan { get; set; } = null!;

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        [ForeignKey(nameof(Tenant))]
        public long TenantId { get; set; }

        /// <summary>Navigation to the owning tenant.</summary>
        public Tenant Tenant { get; set; } = null!;

        /// <summary>Product/card-type id (FK → Products.Id).</summary>
        [ForeignKey(nameof(Product))]
        public long ProductId { get; set; }

        /// <summary>Navigation to the product/card type.</summary>
        public Product Product { get; set; } = null!;

        /// <summary>The batch that introduced this item (FK → Batches.Id). Required.</summary>
        [ForeignKey(nameof(Batch))]
        public long BatchId { get; set; }

        /// <summary>Navigation to the introducing batch. Deleting the batch cascades to its items.</summary>
        public Batch Batch { get; set; } = null!;

        /// <summary>End-customer name; out of MVP scope, kept nullable for future use.</summary>
        public string? CardHolderName { get; set; }

        /// <summary>Card lifecycle status. Batch upload defaults new items to <see cref="CardStatus.Available"/>.</summary>
        public CardStatus Status { get; set; }

        /// <summary>Free-text operational notes.</summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Branch the card physically sits at (FK → Branches.Id), or <c>null</c> when it sits at
        /// no branch at all (Transactions §4.10, decision Q4).
        /// <para>
        /// <c>null</c> covers exactly two situations, with two different statuses:
        /// <list type="bullet">
        ///   <item><description>
        ///     <b>In transit</b> — the card has left the source branch under an in-flight transfer
        ///     and has not been received yet. Paired with <see cref="CardStatus.OnHold"/>. Its
        ///     quantity sits in the holding branch's <c>Stock.HoldQuantity</c>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Unassigned pool</b> — an Unknown-way card that has been received but not yet
        ///     printed. Paired with <see cref="CardStatus.Available"/> (set at ingestion by
        ///     <c>BatchUploadService</c>) — <see cref="CardStatus.OnHold"/> is reserved for the
        ///     in-transit case above, not for a card merely awaiting its first print. Its quantity
        ///     is already counted in the receiving branch's <c>Stock.AvailableQuantity</c>; which
        ///     pool card backs which branch's entitlement is only resolved when the card is printed.
        ///   </description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Invariant, Known way:</b> <c>count(items WHERE BranchID = X AND Status = Available
        /// AND ProductId = P) == Stock[X,P].AvailableQuantity</c>.
        /// <b>Unknown way:</b> that equality does not hold — <c>Stock</c> is the branch's
        /// entitlement and the backing cards live in the tenant-wide unassigned pool.
        /// </para>
        /// <para>
        /// A card with a <c>null</c> branch is immutable to the rest of the system: batch-upload
        /// re-sight, status updates, and soft delete all refuse to touch it, so an in-flight
        /// transfer can never be corrupted from outside the Transactions module.
        /// </para>
        /// </summary>
        [ForeignKey(nameof(Branch))]
        public long? BranchID { get; set; }

        /// <summary>Navigation to the branch, or <c>null</c> while the card sits at no branch.</summary>
        public Branch? Branch { get; set; }
    }
}
