namespace DomainLayer.Entities
{
    /// <summary>
    /// A ribbon type an Evolis print configuration can reference (Printing Module decision Q-05:
    /// converted from a free-text/enum field into a dedicated reference table so new ribbon types
    /// can be catalogued without a code change).
    /// <para>
    /// Confirmed global/tenant-agnostic and non-soft-deletable — the same shape as a lookup
    /// list — since ribbon types (e.g. YMCKO, YMCK, monochrome) describe a physical consumable
    /// standard shared across tenants rather than tenant-specific business data. No
    /// <c>TenantId</c>, no soft delete, no restore workflow.
    /// </para>
    /// </summary>
    public sealed class RibbonType
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>
        /// Ribbon type name (e.g. "YMCKO", "Monochrome K"). Unique, operator-facing, and
        /// renamable (e.g. to a localized display label) — never used as the SDK connection
        /// value. See <see cref="SdkCode"/> for the machine-facing counterpart.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Exact <c>Evolis.RibbonType</c> SDK token (e.g. "YMCKO", "KWHITE") — Evolis Integration
        /// Plan §6, confirmed against the SDK assembly via Phase 0's reflection dump (probe
        /// step 13). Machine-facing, unique, and must never be edited by an operator: unlike
        /// <see cref="Name"/>, a change here would silently break every print job that resolves a
        /// ribbon by this value. Same separation of concerns as <c>Printer.Name</c> vs
        /// <c>Printer.UniqueNumber</c>.
        /// </summary>
        public string SdkCode { get; set; } = string.Empty;
    }
}
