using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Entities
{
    /// <summary>
    /// One product line on a transfer (ERD §4.4, table <c>CardTransferProducts</c>): how many of
    /// a given product were sent, and how the target settled them.
    /// <para>
    /// Append-only alongside its parent (ERD §6.5) — no audit block, no soft delete.
    /// </para>
    /// </summary>
    public class CardTransferProduct
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

        /// <summary>Product id (FK → Products.Id). Unique per transfer.</summary>
        public long ProductId { get; set; }

        /// <summary>Navigation to the product.</summary>
        public Product Product { get; set; } = null!;

        /// <summary>Quantity dispatched from the source branch. Always greater than zero.</summary>
        public int TransactedQuantity { get; set; }

        /// <summary>Quantity accepted at the target, or <c>null</c> until the transfer is settled.</summary>
        public int? RealQuantityReceived { get; set; }

        /// <summary>
        /// Quantity written off at the target instead of being accepted or returned (Addendum A),
        /// or <c>null</c> until settled.
        /// <para>
        /// The returned remainder is deliberately <em>not</em> stored: it is
        /// <c>TransactedQuantity − RealQuantityReceived − DisposedQuantity</c>, and duplicating a
        /// derivable value invites the two to disagree. The database enforces that the three
        /// quantities stay consistent.
        /// </para>
        /// <para>
        /// <b>Schema note:</b> not in ERD §4.4; added with Addendum A (approved).
        /// </para>
        /// </summary>
        public int? DisposedQuantity { get; set; }

        /// <summary>
        /// How this product's cards are tracked, snapshotted from
        /// <see cref="Product.ProductTransactionWay"/> when the transfer was created (ERD §4.4).
        /// <para>
        /// Settlement reads this, never the live product. Product-level immutability (decision P6)
        /// already prevents the value drifting once cards exist, so the snapshot is a second line
        /// of defence rather than the only one — but an in-flight transfer must be settled the way
        /// it was dispatched regardless of anything that happens to the catalog meanwhile.
        /// </para>
        /// </summary>
        public ProductTransactionWay ProductTransactionWay { get; set; }

        /// <summary>
        /// How this line's remainder was resolved at settlement, or <c>null</c> until settled or
        /// when there was no remainder to resolve.
        /// <para>
        /// Meaningful only for an Unknown-way line whose remainder
        /// (<c>TransactedQuantity - RealQuantityReceived</c>) is greater than zero — a Known-way
        /// line's remainder is always resolved per card via <c>CardDispositionEntry</c> instead,
        /// so this stays <c>null</c> for it. Stored per line (not per transfer): a single transfer
        /// can carry more than one Unknown-way line, each settled independently.
        /// </para>
        /// <para>
        /// <b>Schema note:</b> not in the original ERD; added for the Unknown-way Maker-Checker
        /// workflow (approved). Flagged for DBA review, matching every other post-ERD addition to
        /// this aggregate.
        /// </para>
        /// </summary>
        public TransferDifferenceAction? DifferenceAction { get; set; }
    }
}
