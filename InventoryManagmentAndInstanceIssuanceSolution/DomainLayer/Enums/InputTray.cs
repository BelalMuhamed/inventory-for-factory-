namespace DomainLayer.Enums
{
    /// <summary>
    /// An Evolis printer's input tray/feeder destination, used by
    /// <see cref="Entities.EvolisPrinterConfiguration.InputTray"/>. Exact names and numeric
    /// values confirmed by reflecting over the installed <c>Evolis.SDK</c> 9.4.1 assembly
    /// (<c>Evolis.InputTray</c>) and by live round-trip against a connected Evolis Primacy 2
    /// (<c>Connection.GetInputTray()</c> returned <see cref="FEEDER"/>).
    /// <para>
    /// The SDK declares these as bit-flag values (powers of two) because the enum is shared
    /// across every Evolis printer model, some of which can report a tray as a combination of
    /// flags. The Primacy 2 does not: setting a combined value
    /// (<c>FEEDER | BEZEL</c>) was rejected live with <c>EUNSUPPORTED</c>, and get/set always
    /// round-tripped a single member. Persisted as a single value, not a <c>[Flags]</c>
    /// bitmask — via <c>HasConversion&lt;short&gt;()</c> (not <c>byte</c>: <see cref="OutputTray"/>'s
    /// largest member, 256, does not fit in a <c>tinyint</c>, and both trays share the same
    /// column-width decision for consistency).
    /// </para>
    /// </summary>
    public enum InputTray : short
    {
        /// <summary>The standard input feeder. Default. Confirmed live on a connected Primacy 2.</summary>
        FEEDER = 1,

        /// <summary>Manual single-card insertion.</summary>
        MANUAL = 2,

        /// <summary>Manual insertion, command-driven.</summary>
        MANUALANDCMD = 4,

        /// <summary>The bezel (front slot) tray.</summary>
        BEZEL = 8,

        /// <summary>Both feeder and bezel.</summary>
        BOTH = 16,

        /// <summary>No feeder present.</summary>
        NOFEEDER = 32,

        /// <summary>Rear input tray, where the printer model supports one.</summary>
        REAR = 64
    }
}
