using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Entities
{
    /// <summary>
    /// One card written off under a <see cref="CardDisposal"/> (API §4.10, Addendum A).
    /// <para>
    /// Rows exist for every disposal regardless of the product's transaction way. Unknown-way
    /// disposal still names the specific cards it consumed — the caller does not choose them
    /// (the system takes them FIFO), but the record of which cards left inventory is exact either
    /// way. Without that, a disposal would reduce a quantity with nothing to reconcile against.
    /// </para>
    /// </summary>
    public class CardDisposalItem
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Denormalized from the parent for scoping.</summary>
        public long TenantId { get; set; }

        /// <summary>Owning disposal id (FK → CardDisposals.Id, cascade).</summary>
        public long CardDisposalId { get; set; }

        /// <summary>Navigation to the owning disposal.</summary>
        public CardDisposal CardDisposal { get; set; } = null!;

        /// <summary>The card written off (FK → ProductItems.Id). Unique per disposal.</summary>
        public long ProductItemId { get; set; }

        /// <summary>Navigation to the card.</summary>
        public ProductItem ProductItem { get; set; } = null!;
    }
}
