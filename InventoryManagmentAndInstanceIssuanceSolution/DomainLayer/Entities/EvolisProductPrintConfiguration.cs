using DomainLayer.Common;
using DomainLayer.Enums;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Evolis printing parameters for a product (ERD §7.1, table
    /// <c>EvolisProductPrintConfigurations</c>; Printing Module decisions Q-02/Q-05). Exactly one
    /// row per product among non-deleted rows (decision Q-02). <see cref="PrintedFace"/> is
    /// retained as a plain descriptive column (which side of the card this design targets) — it
    /// is not part of the table's key or cardinality.
    /// <para>
    /// <see cref="RibbonTypeId"/> replaces a free-text/enum ribbon type per decision Q-05.
    /// </para>
    /// <para>
    /// Lifecycle follows the owning <see cref="Product"/> as a single aggregate — see the
    /// parallel remarks on <see cref="MaticaProductPrintConfiguration"/>.
    /// </para>
    /// </summary>
    public sealed class EvolisProductPrintConfiguration : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant id (FK → Tenants.Id). Denormalized from <see cref="Product"/> for direct tenant-scoped queries.</summary>
        public long TenantId { get; set; }

        /// <summary>The product this configuration belongs to (FK → Products.Id). Unique per tenant among non-deleted rows.</summary>
        public long ProductId { get; set; }

        /// <summary>
        /// Navigation to the owning product. See the parallel remarks on
        /// <see cref="MaticaProductPrintConfiguration.Product"/> — same reason, same mechanism.
        /// </summary>
        public Product Product { get; set; } = null!;

        /// <summary>Ribbon type reference (FK → RibbonTypes.Id; decision Q-05).</summary>
        public long RibbonTypeId { get; set; }

        /// <summary>Card orientation for printing.</summary>
        public PrintWay PrintWay { get; set; }

        /// <summary>Horizontal print position.</summary>
        public int X { get; set; }

        /// <summary>Vertical print position.</summary>
        public int Y { get; set; }

        /// <summary>Which face of the card this design targets. Descriptive only — not part of the table's key (decision Q-02).</summary>
        public PrintedFace PrintedFace { get; set; }

        /// <summary>Font family name.</summary>
        public string FontFamily { get; set; } = string.Empty;

        /// <summary>Font size in points.</summary>
        public int FontSize { get; set; }

        /// <summary>Text color as a HEX value (e.g. <c>#FFFFFF</c>).</summary>
        public string PrintColor { get; set; } = string.Empty;

        /// <summary>Background color as a HEX value (e.g. <c>#000000</c>).</summary>
        public string BackgroundColor { get; set; } = string.Empty;

        /// <summary>Font style (e.g. "Bold", "Italic").</summary>
        public string FontStyle { get; set; } = string.Empty;

        /// <summary>
        /// The print image for this configuration (FK → PrintImages.Id). Null until an image has
        /// been uploaded and attached. Replaces the earlier bare <c>ImagePath</c> string (single
        /// source of truth: <see cref="PrintImage"/> owns the image's metadata and physical
        /// location; this table only references it by id).
        /// </summary>
        public long? ImageId { get; set; }
    }
}
