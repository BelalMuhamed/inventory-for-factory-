using System;
using DomainLayer.Common;
using DomainLayer.Enums;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Evolis-specific machine configuration (Evolis Integration Plan §6, table
    /// <c>EvolisPrinterConfigurations</c>). A 1:1 extension of <see cref="Printer"/>, mirroring
    /// <see cref="MaticaPrinterConfiguration"/> exactly — same aggregate as its owning
    /// <see cref="Printer"/> (not a cross-aggregate reference), <see cref="DeleteBehavior.Cascade"/>
    /// on the FK, filtered unique index, soft-delete query filter.
    /// <para>
    /// Supersedes the doc-comment claim in <c>AppDbContext.ConfigurePrinterRegistry</c> that
    /// "Evolis needs no extension table at all (module requirement §1)" — decision Q-15
    /// overrides module requirement §1; see plan §1 and R-10.
    /// </para>
    /// <para>
    /// <see cref="DeviceName"/> is the SDK connection key: the exact string
    /// <c>Evolis.GetDevices()</c> returns as <c>Device.name</c>, and the string
    /// <c>new Connection(name, mode)</c> consumes to address the printer. Without it the Agent
    /// cannot open a connection at all.
    /// </para>
    /// </summary>
    public sealed class EvolisPrinterConfiguration : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Denormalized, matching this codebase's convention for a tenant-owned child row.</summary>
        public long TenantId { get; set; }

        /// <summary>Owning printer id (FK → Printers.Id). Unique — enforces the 1:1 relationship.</summary>
        public long PrinterId { get; set; }

        /// <summary>
        /// Navigation to the owning printer. Set this (not <see cref="PrinterId"/> directly) when
        /// creating a new Evolis printer, so EF Core can fix up the foreign key from the printer's
        /// generated identity once the single <c>SaveChanges</c> call runs — the same mechanism
        /// <see cref="MaticaPrinterConfiguration.Printer"/> already relies on.
        /// </summary>
        public Printer Printer { get; set; } = null!;

        /// <summary>
        /// The SDK connection key — must exactly match <c>Evolis.Device.name</c> for this
        /// printer. See the class remarks.
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// Hostname of the machine whose Agent last self-registered this printer as supervised
        /// (decision Q-21). Null until first discovery; written only by Phase 2's self-registration
        /// route, never by this phase's create/update endpoints.
        /// </summary>
        public string? SupervisorHostName { get; set; }

        /// <summary>Timestamp of the last successful self-registration — staleness indicator for the UI.</summary>
        public DateTime? SupervisorRegisteredAt { get; set; }

        /// <summary>Printer-level default ribbon type (FK → RibbonTypes.Id), used when a product's own print configuration doesn't specify one.</summary>
        public long? DefaultRibbonTypeId { get; set; }

        /// <summary>Magnetic-stripe coercivity for this printer's encoder. Default <see cref="MagCoercivity.PRINTER"/>.</summary>
        public MagCoercivity MagCoercivity { get; set; } = MagCoercivity.PRINTER;

        /// <summary>Input tray. Default <see cref="InputTray.FEEDER"/>.</summary>
        public InputTray InputTray { get; set; } = InputTray.FEEDER;

        /// <summary>Output tray. Default <see cref="OutputTray.STANDARD"/>.</summary>
        public OutputTray OutputTray { get; set; } = OutputTray.STANDARD;

        /// <summary>Reject/error tray — same type as <see cref="OutputTray"/> (confirmed via the SDK's
        /// own <c>GetErrorTray()</c>/<c>SetErrorTray(Evolis.OutputTray)</c> signatures), not a
        /// separate type. Default <see cref="OutputTray.ERROR"/>.</summary>
        public OutputTray ErrorTray { get; set; } = OutputTray.ERROR;

        /// <summary>
        /// SDK <c>Connection</c> open mode (decision Q-29). Default
        /// <see cref="EvolisOpenMode.SUPERVISED"/> — confirmed by Phase 0 hardware testing; see
        /// that enum's remarks.
        /// </summary>
        public EvolisOpenMode OpenMode { get; set; } = EvolisOpenMode.SUPERVISED;

        /// <summary>SDK error-management mode (decision Q-29). Default <see cref="EvolisErrorManagement.SOFTWARE"/>.</summary>
        public EvolisErrorManagement ErrorManagement { get; set; } = EvolisErrorManagement.SOFTWARE;
    }
}
