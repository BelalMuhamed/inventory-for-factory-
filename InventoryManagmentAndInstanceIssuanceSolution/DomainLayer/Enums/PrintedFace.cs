namespace DomainLayer.Enums
{
    /// <summary>
    /// Which physical face of the card a print element applies to (ERD §7.1, Printing Module
    /// decisions Q-02/Q-04/Q-06). Persisted as <c>TINYINT</c>.
    /// <para>
    /// Present only on <see cref="Entities.EvolisProductPrintConfiguration"/> — the Matica
    /// configuration deliberately omits it per decision Q-04. On the Evolis table it is a plain
    /// descriptive column only: decision Q-02 locks exactly one print-configuration row per
    /// product regardless of face, so this value is never part of either table's key or
    /// cardinality.
    /// </para>
    /// </summary>
    public enum PrintedFace : byte
    {
        /// <summary>Front of the card. Value 0.</summary>
        Front = 0,

        /// <summary>Back of the card. Value 1.</summary>
        Back = 1
    }
}
