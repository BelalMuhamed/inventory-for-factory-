namespace DomainLayer.Enums
{
    /// <summary>
    /// Per-card outcome inside a Known-way transfer (ERD §8, extended by Addendum A). Persisted
    /// as <c>TINYINT</c>. Only meaningful for products whose snapshotted
    /// <see cref="ProductTransactionWay"/> is <see cref="ProductTransactionWay.Known"/> — an
    /// Unknown-way line moves quantities and writes no item rows at all.
    /// </summary>
    public enum TransactionItemReceiveStatus : byte
    {
        /// <summary>In flight; the target has not settled this card yet. ERD value 0.</summary>
        Pending = 0,

        /// <summary>Received at the target branch; the card is now pinned there. ERD value 1.</summary>
        Received = 1,

        /// <summary>
        /// Not received. The card is going back to the source under the auto-generated return
        /// transfer, where it gets a fresh item row of its own. ERD value 2.
        /// </summary>
        NotReceived = 2,

        /// <summary>
        /// Written off at the target instead of being received or returned (Addendum A). The card
        /// moves to <see cref="CardStatus.Disposed"/> and leaves inventory; a
        /// <c>CardDisposal</c> record carries the mandatory reason.
        /// <para>
        /// <b>Schema note:</b> not in the original ERD §8 list; added with Addendum A (approved).
        /// </para>
        /// </summary>
        Disposed = 3
    }
}
