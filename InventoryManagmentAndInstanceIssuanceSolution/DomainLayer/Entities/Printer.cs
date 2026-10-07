using DomainLayer.Common;
using DomainLayer.Enums;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A physical printer registered to a branch (ERD §6.1, table <c>Printers</c>; Printing
    /// Module decision Q-01). Holds the information common to both printer families — Matica and
    /// Evolis printers share this row; only Matica additionally extends it with a
    /// <see cref="MaticaPrinterConfiguration"/> row via a 1:1 relationship on <see cref="Id"/>.
    /// Evolis requires no extension row at all (module requirement §1). Soft-deletable through
    /// the inherited audit fields.
    /// <para>
    /// Read-only for tenant users (decision Q-09): tenant callers may list/filter printers by
    /// <see cref="UsingPrinterType"/> and <see cref="BranchId"/>, but only a system admin may
    /// create, update, or delete a row here.
    /// </para>
    /// </summary>
    public sealed class Printer : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id).</summary>
        public long TenantId { get; set; }

        /// <summary>Branch this printer is physically located at (FK → Branches.Id).</summary>
        public long BranchId { get; set; }

        /// <summary>
        /// Navigation to the owning branch. Added in P5 (Printer registry service) so
        /// <c>PrinterRepo</c> can eager-load it for <c>PrinterResponse.BranchName</c> — the same
        /// reasoning as <see cref="BranchRequestItem.Product"/>. Metadata only: no new column, no
        /// migration required beyond what P1 already produced.
        /// </summary>
        public Branch Branch { get; set; } = null!;

        /// <summary>Printer family (ERD §6.1, §8). Drives which extension table, if any, applies.</summary>
        public UsingPrinterType UsingPrinterType { get; set; }

        /// <summary>
        /// Friendly display name for the printer, set by the system admin at registration.
        /// <para>
        /// ERD §6.1 lists only <see cref="UsingPrinterType"/>, <see cref="UniqueNumber"/>, and
        /// <see cref="BranchId"/> on <c>Printers</c>. <see cref="Name"/> and <see cref="Model"/>
        /// are carried over from the module requirements' original
        /// <c>MaticaPrinterConfigurations</c> field list and relocated here — confirmed placement
        /// per decision Q-01, since <c>Printers</c> "stores the common printer information for
        /// both Matica and Evolis printers."
        /// </para>
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Hardware model identifier. See the placement note on <see cref="Name"/>.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Serial number / unique hardware identifier for the printer, for both Evolis and Matica
        /// printers alike. Unique per tenant among non-deleted rows.
        /// <para>
        /// Not a network address — a Matica printer's IP address lives on its own dedicated
        /// <see cref="MaticaPrinterConfiguration.IpAddress"/> field instead. This field's doc
        /// comment previously read "Serial number for an Evolis printer, or IP address for a
        /// Matica printer" (matching the ERD's original wording), which conflated two different
        /// facts about a Matica printer under one property; corrected once <c>IpAddress</c> was
        /// introduced to hold the network address on its own.
        /// </para>
        /// </summary>
        public string UniqueNumber { get; set; } = string.Empty;
    }
}
