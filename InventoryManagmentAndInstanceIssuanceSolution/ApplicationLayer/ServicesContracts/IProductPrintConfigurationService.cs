using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// The product print-configuration sub-resource use case (decision Q-07:
    /// <c>GET</c>/<c>PUT /api/products/{id}/print-config</c>, no standalone POST/DELETE — the
    /// configuration's create/delete lifecycle stays with <c>ProductService</c>, per the
    /// single-aggregate design). Built on <c>IProductPrintConfigComposer</c> for the actual
    /// validation and staging rules, the same way <c>ITransferService</c> is built on
    /// <c>ITransferComposer</c>.
    /// </summary>
    public interface IProductPrintConfigurationService
    {
        /// <summary>Returns the print configuration for a product, scoped to the caller.</summary>
        Task<Result<ProductPrintConfigResponse>> GetForProductAsync(
            long productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a product's print configuration. System-admin only — decision Q-09,
        /// confirmed, extends to this endpoint as well as the product-plus-configuration create
        /// flow and printer-registry writes. Fails with
        /// <c>PrintingErrors.ProductPrintConfigOnlySystemAdmin</c> for a tenant caller. Supplying
        /// a different <see cref="UpdateProductPrintConfigRequest.UsingPrinterType"/> than the
        /// product currently has switches its printer family (decision Q-08): the old
        /// configuration row is hard deleted and the new one inserted, in one transaction.
        /// </summary>
        Task<Result<ProductPrintConfigResponse>> UpdateForProductAsync(
            long productId, UpdateProductPrintConfigRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a product together with its print configuration in one call
        /// (<c>GET /api/products/{id}/print-config/full</c>, Printing Module phase 7).
        /// System-admin only — fails with <c>PrintingErrors.ProductPrintConfigOnlySystemAdmin</c>
        /// for a tenant caller, and bypasses tenant scoping entirely for the admin (matching
        /// every other admin-only read in this module). Unlike <see cref="GetForProductAsync"/>,
        /// a product with no configuration yet is not an error here — the response's print
        /// configuration is <c>null</c> rather than the call failing, since this endpoint exists
        /// specifically as an administrative overview that should work at any point in a
        /// product's lifecycle.
        /// </summary>
        Task<Result<ProductWithPrintConfigResponse>> GetProductWithConfigAsync(
            long productId, CancellationToken cancellationToken = default);
    }
}
