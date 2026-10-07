namespace DomainLayer.Enums
{
    /// <summary>
    /// How a product's items are tracked when moved between branches (ERD §8). Persisted as
    /// <c>TINYINT</c>. A transaction snapshots this value at creation time (ERD §4.4).
    /// </summary>
    public enum ProductTransactionWay : byte
    {
        /// <summary>Individual items are known and tracked per <c>ProductItem</c>. ERD value 0.</summary>
        Known = 0,

        /// <summary>Only quantities are tracked; individual items are not enumerated. ERD value 1.</summary>
        Unknown = 1
    }
}
