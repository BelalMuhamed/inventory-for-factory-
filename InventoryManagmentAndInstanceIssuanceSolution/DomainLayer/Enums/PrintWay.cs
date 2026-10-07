namespace DomainLayer.Enums
{
    /// <summary>
    /// Card orientation for Evolis printing (ERD §7.1, Printing Module decision Q-06). Persisted
    /// as <c>TINYINT</c>, matching every other closed enum in this codebase (see
    /// <see cref="UsingPrinterType"/>, <see cref="ActivationStatus"/>).
    /// </summary>
    public enum PrintWay : byte
    {
        /// <summary>Card printed in landscape orientation. Value 0.</summary>
        Landscape = 0,

        /// <summary>Card printed in portrait orientation. Value 1.</summary>
        Portrait = 1
    }
}
