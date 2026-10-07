using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Entities
{
    /// <summary>
    /// One individually tracked card on a transfer (ERD §4.5, table <c>CardTransferItems</c>).
    /// <para>
    /// <b>Correction, superseding an earlier (incorrect) note on this type:</b> rows exist only
    /// for Known-way product lines. An Unknown-way line moves <c>Stock</c> entitlement alone — no
    /// <c>ProductItem</c> is ever selected, touched, or reassigned for it, so there is nothing for
    /// a row here to reference. This holds both before and after the Unknown-way Maker-Checker
    /// workflow: an Unknown-way line's create-time Hold and its later receive-time settlement
    /// (<see cref="Enums.TransferDifferenceAction"/>) are both expressed purely as quantities on
    /// <see cref="CardTransferProduct"/>. ERD §8's "individual items are not enumerated" describes
    /// exactly this: an Unknown-way line accepts no <c>productItemIds</c> from the caller and
    /// carries no item rows internally either, matching <c>CardTransfer.Items</c>' own doc
    /// comment.
    /// </para>
    /// <para>
    /// A card returned by a partial receipt gets a <em>fresh</em> row on the auto-generated return
    /// transfer; this row stays as the historical record of the outbound leg, marked
    /// <see cref="TransactionItemReceiveStatus.NotReceived"/>.
    /// </para>
    /// </summary>
    public class CardTransferItem
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Denormalized from the parent for scoping.</summary>
        public long TenantId { get; set; }

        /// <summary>Owning transfer id (FK → CardsTransferHistory.Id, cascade).</summary>
        public long CardTransferId { get; set; }

        /// <summary>Navigation to the owning transfer.</summary>
        public CardTransfer CardTransfer { get; set; } = null!;

        /// <summary>The card being moved (FK → ProductItems.Id). Unique per transfer.</summary>
        public long ProductItemId { get; set; }

        /// <summary>Navigation to the card.</summary>
        public ProductItem ProductItem { get; set; } = null!;

        /// <summary>Per-card outcome. Starts <see cref="TransactionItemReceiveStatus.Pending"/>.</summary>
        public TransactionItemReceiveStatus ReceiveStatus { get; set; } = TransactionItemReceiveStatus.Pending;
    }
}
