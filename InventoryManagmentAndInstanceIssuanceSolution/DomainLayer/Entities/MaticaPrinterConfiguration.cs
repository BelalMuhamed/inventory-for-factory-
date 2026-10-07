using DomainLayer.Common;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Matica-specific machine configuration (ERD §6.2, table
    /// <c>MaticaPrinterConfigurations</c>; Printing Module decision Q-01). A 1:1 extension of
    /// <see cref="Printer"/> — every Matica printer has exactly one row here, keyed by
    /// <see cref="PrinterId"/>; Evolis printers have none, since Evolis requires no server-side
    /// machine configuration (module requirement §1).
    /// <para>
    /// Belongs to the same aggregate as its owning <see cref="Printer"/> (not a cross-aggregate
    /// reference) — deleting the printer removes this row too.
    /// </para>
    /// </summary>
    public sealed class MaticaPrinterConfiguration : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning printer id (FK → Printers.Id). Unique — enforces the 1:1 relationship.</summary>
        public long PrinterId { get; set; }

        /// <summary>
        /// Navigation to the owning printer. Added in P5: <c>PrinterConfigurationService</c>
        /// inserts a new printer and its Matica configuration in the same
        /// <c>ExecuteInTransactionAsync</c> call, so <see cref="PrinterId"/> does not exist yet
        /// when the configuration object is built — EF Core needs this navigation to fix up the
        /// foreign key from the printer's generated identity once the single <c>SaveChanges</c>
        /// call actually runs. Set <see cref="Printer"/> (not <see cref="PrinterId"/> directly)
        /// when creating a new Matica printer; the reverse is fine once the printer already has a
        /// real id. Metadata only: no new column, no migration required beyond what P1 already
        /// produced.
        /// </summary>
        public Printer Printer { get; set; } = null!;

        /// <summary>Matica feeder identifier.</summary>
        public int FeederId { get; set; }

        /// <summary>Matica hopper identifier.</summary>
        public int HopperId { get; set; }

        /// <summary>Matica reject-bin identifier.</summary>
        public int RejectedId { get; set; }

        /// <summary>Communication port (e.g. COM port or TCP port) the printer listens on.</summary>
        public string Port { get; set; } = string.Empty;

        /// <summary>
        /// The physical Matica machine's actual network IP address — the address used when the
        /// backend (or, in practice today, the Matica Printer Agent it authorizes) needs to
        /// communicate with the physical machine. Deliberately separate from
        /// <see cref="Printer.UniqueNumber"/>, which is the machine's serial/unique identifier and
        /// nothing else, for both printer families. Admin-managed only, same trust boundary as
        /// every other field on this entity (<see cref="PrinterId"/>'s own doc comment) — no print
        /// request, print result, or other runtime traffic writes to this table at all; confirmed
        /// by inspection, not assumed, before this field was added.
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Tipper temperature, pressure, consumption and time settings sent with every Emboss
        /// command by the Matica Printer Agent (Matica Print Flow, tipper-parameter phase).
        /// Printer-level hardware calibration, not per-product — hence living here rather than on
        /// <see cref="MaticaProductPrintConfiguration"/>. Default <c>0</c> when not explicitly
        /// configured is intentional (approved decision): a Matica printer that has never had
        /// these set should behave exactly as it did before this phase existed, not fail or
        /// substitute an assumed value.
        /// </summary>
        public int TipperTemperature { get; set; }

        /// <summary>Tipper pressure setting. See <see cref="TipperTemperature"/> for the default-0 rationale.</summary>
        public int TipperPressure { get; set; }

        /// <summary>Tipper consumption setting. See <see cref="TipperTemperature"/> for the default-0 rationale.</summary>
        public int TipperConsumption { get; set; }

        /// <summary>Tipper time setting. See <see cref="TipperTemperature"/> for the default-0 rationale.</summary>
        public int TipperTime { get; set; }
    }
}
