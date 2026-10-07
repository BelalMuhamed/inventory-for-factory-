namespace DomainLayer.Enums
{
    /// <summary>
    /// How an Unknown-way transfer line's unreceived remainder was resolved at settlement
    /// (Unknown-way Maker-Checker workflow). Persisted as <c>TINYINT</c>.
    /// <para>
    /// Meaningful only for a line whose snapshotted <see cref="ProductTransactionWay"/> is
    /// <see cref="ProductTransactionWay.Unknown"/> and whose settlement leaves a remainder
    /// (<c>TransactedQuantity - RealQuantityReceived &gt; 0</c>). A Known-way line resolves its
    /// remainder per card via <c>CardDispositionEntry</c> instead - the two mechanisms are not
    /// interchangeable, since a Known-way line's cards are always individually accounted for.
    /// </para>
    /// <para>
    /// <b>Schema note:</b> not in the original ERD; added for the Unknown-way Maker-Checker
    /// workflow (approved). Flagged for DBA review, matching the precedent of every other
    /// post-ERD addition to this aggregate.
    /// </para>
    /// </summary>
    public enum TransferDifferenceAction : byte
    {
        /// <summary>
        /// The remainder was never dispatched in substance - since an Unknown-way line moves
        /// entitlement rather than physical cards, it is credited straight back to the source
        /// branch's stock through an auto-generated return transfer, mirroring how a Known-way
        /// line's unreceived remainder is returned. ERD-equivalent value 0.
        /// </summary>
        ReturnedToSource = 0,

        /// <summary>
        /// The remainder is assigned to the target branch anyway, despite only part of it being
        /// physically/operationally confirmed - the target's stock is credited in full
        /// (<c>TransactedQuantity</c>), while <see cref="CardTransferProduct.RealQuantityReceived"/>
        /// keeps the true confirmed quantity, so the discrepancy stays visible rather than
        /// silently absorbed. ERD-equivalent value 1.
        /// </summary>
        KeptAtDestination = 1
    }
}
