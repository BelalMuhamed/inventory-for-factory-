namespace DomainLayer.Enums
{
    /// <summary>
    /// Printer family a product is printed on (ERD §8). Shared with Printers (ERD §6) and the
    /// print-configuration tables (ERD §7). Persisted as <c>TINYINT</c>.
    /// </summary>
    public enum UsingPrinterType : byte
    {
        /// <summary>Evolis printer family. ERD value 0.</summary>
        Evolis = 0,

        /// <summary>Matica printer family. ERD value 1.</summary>
        Matica = 1
    }
}
