namespace DomainLayer.Enums
{
    /// <summary>
    /// An Evolis printer's output tray/destination. Shared by
    /// <see cref="Entities.EvolisPrinterConfiguration.OutputTray"/> and
    /// <see cref="Entities.EvolisPrinterConfiguration.ErrorTray"/> — confirmed by reflecting over
    /// the installed <c>Evolis.SDK</c> 9.4.1 assembly, where <c>Connection.GetErrorTray()</c> /
    /// <c>SetErrorTray(Evolis.OutputTray)</c> are themselves typed <c>Evolis.OutputTray</c>, not a
    /// separate error-tray type. Both confirmed live against a connected Evolis Primacy 2:
    /// <c>GetOutputTray()</c> returned <see cref="STANDARD"/>, <c>GetErrorTray()</c> returned
    /// <see cref="ERROR"/>.
    /// <para>
    /// See <see cref="InputTray"/>'s remarks for why these are bit-flag-shaped values but persisted
    /// as a single value via <c>HasConversion&lt;short&gt;()</c>, not a <c>[Flags]</c> bitmask: the
    /// Primacy 2 rejects combined values at the SDK level.
    /// </para>
    /// </summary>
    public enum OutputTray : short
    {
        /// <summary>The standard output stacker. Default. Confirmed live on a connected Primacy 2.</summary>
        STANDARD = 1,

        /// <summary>Standard tray, standby variant.</summary>
        STANDARDSTANDBY = 2,

        /// <summary>Manual single-card output.</summary>
        MANUAL = 4,

        /// <summary>The reject/error bin. Default for <c>ErrorTray</c>. Confirmed live on a connected Primacy 2.</summary>
        ERROR = 8,

        /// <summary>Error tray, standby variant.</summary>
        ERRORSTANDBY = 16,

        /// <summary>Eject tray.</summary>
        EJECT = 32,

        /// <summary>The bezel (front slot) tray.</summary>
        BEZEL = 64,

        /// <summary>Error slot tray.</summary>
        ERRORSLOT = 128,

        /// <summary>Locked tray.</summary>
        LOCKED = 256
    }
}
