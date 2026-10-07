namespace DomainLayer.Enums
{
    /// <summary>
    /// Lifecycle state of a card transfer (ERD §8, extended by API §4.10 decisions Q5 and
    /// Addendum A). Persisted as <c>TINYINT</c>.
    /// <para>
    /// <see cref="InProgress"/> is the only non-terminal value. Every other value closes the
    /// transfer permanently — a remainder that still has to move continues its life in a
    /// separate, auto-generated return transfer rather than reopening this one.
    /// </para>
    /// </summary>
    public enum TransactionStatus : byte
    {
        /// <summary>
        /// Created and dispatched; the quantity sits in the source branch's hold and the cards
        /// have left that branch (<c>BranchID IS NULL</c>). ERD value 0.
        /// </summary>
        InProgress = 0,

        /// <summary>Every transacted card was received at the target. ERD value 1.</summary>
        Received = 1,

        /// <summary>
        /// Nothing was received and nothing was disposed: the whole quantity is going back, under
        /// an auto-generated return transfer. ERD value 2.
        /// <para>
        /// Reachable only through <c>receive</c> with zero received and zero disposed — the
        /// separate <c>refuse</c> endpoint was removed in favour of the disposition model, so this
        /// is now the "rejected in full" outcome rather than a distinct workflow.
        /// </para>
        /// </summary>
        ReturnedBack = 2,

        /// <summary>
        /// Some cards were received and/or disposed at the target, and the rest (if any) moved to
        /// an auto-generated return transfer. Terminal for <em>this</em> transfer.
        /// <para>
        /// <b>Schema note:</b> not in the original ERD §8 list; added with decision Q5 (approved).
        /// Appended as 3 so the ERD's own 0/1/2 keep their meaning.
        /// </para>
        /// <para>
        /// Per-product "FullyReceived / PartialReceived" remains a query-time DTO computation over
        /// <c>TransactedQuantity</c> vs <c>RealQuantityReceived</c> (ERD §4.5) — never a column.
        /// </para>
        /// </summary>
        PartiallyReceived = 3,

        /// <summary>
        /// The whole remaining quantity was written off by the holding party instead of being
        /// received or returned (Addendum A). Terminal, and the outcome that stops a return leg
        /// from bouncing between two branches indefinitely.
        /// <para>
        /// <b>Schema note:</b> not in the original ERD §8 list; added with Addendum A (approved).
        /// </para>
        /// </summary>
        Disposed = 4
    }
}
