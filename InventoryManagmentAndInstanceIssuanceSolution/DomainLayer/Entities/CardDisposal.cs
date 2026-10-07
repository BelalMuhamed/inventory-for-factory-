using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A write-off: cards permanently removed from inventory, with the reason and the responsible
    /// branch recorded (API §4.10, Addendum A).
    /// <para>
    /// <b>Why this is its own aggregate rather than columns on <c>ProductItem</c>.</b> A single
    /// operational event ("a box arrived water-damaged") covers many cards. Storing the reason per
    /// card would repeat it N times and give no way to ask "what did branch X write off last
    /// quarter, and why" without scraping audit JSON. The header also gives §4.14 reporting a
    /// natural place to hang off later.
    /// </para>
    /// <para>
    /// Append-only, like transfers: no audit block, no soft delete, never updated after creation.
    /// Disposal is the one operation in the system that destroys quantity, so it is deliberately
    /// irreversible — a mistaken disposal is corrected by a compensating inbound batch, not by
    /// undoing history.
    /// </para>
    /// </summary>
    public class CardDisposal
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        public long TenantId { get; set; }

        /// <summary>
        /// Navigation to the owning tenant. Present so that this and
        /// <see cref="DisposedByTenant"/> configure as two distinct relationships — EF Core
        /// identifies a relationship by its navigations, so two navigation-less
        /// <c>HasOne&lt;Tenant&gt;().WithMany()</c> calls would silently reconfigure the same one.
        /// </summary>
        public Tenant Tenant { get; set; } = null!;

        /// <summary>
        /// Branch that performed the disposal and whose stock was decremented (FK → Branches.Id).
        /// <para>
        /// Required, and supplied explicitly by the caller rather than derived: cards being
        /// disposed mid-transfer have no branch of their own
        /// (<c>ProductItem.BranchID IS NULL</c>), so there is nothing to derive it from.
        /// </para>
        /// </summary>
        public long BranchId { get; set; }

        /// <summary>Navigation to the disposing branch.</summary>
        public Branch Branch { get; set; } = null!;

        /// <summary>
        /// The transfer this disposal settled (FK → CardsTransferHistory.Id), or <c>null</c> when
        /// cards sitting at a branch were written off outside any transfer.
        /// </summary>
        public long? CardTransferId { get; set; }

        /// <summary>Navigation to the settled transfer, or <c>null</c>.</summary>
        public CardTransfer? CardTransfer { get; set; }

        /// <summary>
        /// Tenant that performed the disposal (FK → Tenants.Id). Non-nullable: a system admin may
        /// never dispose cards, so there is always a tenant to record.
        /// </summary>
        public long DisposedByTenantId { get; set; }

        /// <summary>Navigation to the disposing tenant.</summary>
        public Tenant DisposedByTenant { get; set; } = null!;

        /// <summary>UTC instant of the write-off.</summary>
        public DateTime DisposedAt { get; set; }

        /// <summary>
        /// Why the cards were written off. Mandatory and non-empty — an unexplained write-off is
        /// indistinguishable from stock going missing, which is exactly what this record exists to
        /// rule out.
        /// </summary>
        public string Reason { get; set; } = null!;

        /// <summary>The cards written off. At least one always exists.</summary>
        public List<CardDisposalItem> Items { get; set; } = new();
    }
}
