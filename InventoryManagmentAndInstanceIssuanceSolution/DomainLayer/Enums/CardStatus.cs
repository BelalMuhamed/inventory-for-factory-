namespace DomainLayer.Enums
{
    /// <summary>
    /// Lifecycle state of a single card instance (ERD §8, extended by the Transactions module).
    /// Persisted as <c>TINYINT</c>.
    /// <para>
    /// Read together with <see cref="Entities.ProductItem.BranchID"/>: a card whose branch is
    /// <c>null</c> is not at any branch, and the only statuses valid in that situation are
    /// <see cref="OnHold"/> (in transit under an active transfer), <see cref="Available"/>
    /// (received-but-unassigned for an Unknown-way product, awaiting its first print), and
    /// <see cref="Disposed"/>.
    /// </para>
    /// </summary>
    public enum CardStatus
    {
        /// <summary>
        /// Not available for issue because the card is actively moving between branches under an
        /// in-flight transfer (branch is <c>null</c> for the duration). Not used for an
        /// Unknown-way card merely awaiting its first print — that case is <see cref="Available"/>
        /// instead (see this enum's own remarks, and <see cref="Entities.ProductItem.BranchID"/>'s).
        /// ERD value 0.
        /// </summary>
        OnHold = 0,

        /// <summary>
        /// Issuable right now. Counted in a branch's <c>Stock.AvailableQuantity</c>. Normally
        /// paired with a non-null branch (pinned there) — the one exception is an Unknown-way
        /// card sitting in the tenant-wide unassigned pool (branch <c>null</c>) awaiting a branch
        /// assignment at print time; its quantity is still counted in the <em>named</em> branch's
        /// <c>Stock.AvailableQuantity</c> even though the row itself isn't pinned there yet. ERD
        /// value 1.
        /// </summary>
        Available = 1,

        /// <summary>Successfully printed and issued to an end customer. ERD value 2.</summary>
        SuccessPrinted = 2,

        /// <summary>Printing failed; the physical card is spoiled. ERD value 3.</summary>
        FailedPrinting = 3,

        /// <summary>Past its usable date. ERD value 4.</summary>
        Expired = 4,

        /// <summary>
        /// Written off and permanently removed from inventory (Transactions §4.10, Addendum A).
        /// Terminal: a disposed card is never restored, re-sighted, or transferred again, and its
        /// quantity is not counted in any <c>Stock</c> column. Every disposal is backed by a
        /// <c>CardDisposal</c> record carrying the mandatory reason and the disposing branch.
        /// <para>
        /// <b>Schema note:</b> not present in the original ERD §8 enum list; added with the
        /// Transactions module (approved). Flagged for DBA review.
        /// </para>
        /// </summary>
        Disposed = 5
    }
}
