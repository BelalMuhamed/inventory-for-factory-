namespace DomainLayer.Enums
{
    /// <summary>
    /// Magnetic-stripe coercivity setting for an Evolis printer's mag encoder (Evolis
    /// Integration Plan §6, decision Q-29-adjacent config). Persisted as <c>tinyint</c> via
    /// <c>HasConversion&lt;byte&gt;()</c>, matching this codebase's convention for every other
    /// persisted enum.
    /// </summary>
    public enum MagCoercivity : byte
    {
        /// <summary>Use the printer's own configured default rather than overriding it per session.</summary>
        PRINTER = 0,

        /// <summary>Let the SDK auto-detect coercivity per card.</summary>
        AUTO = 1,

        /// <summary>Low coercivity.</summary>
        LOCO = 2,

        /// <summary>High coercivity.</summary>
        HICO = 3
    }
}
