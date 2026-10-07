using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.DTOs.Products;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Default <see cref="IProductPrintConfigurationService"/> implementation (decision Q-07).
    /// Built on <see cref="IProductPrintConfigComposer"/> for the actual validation and staging
    /// rules, the same way <c>TransferService</c> is built on <c>ITransferComposer</c>.
    /// </summary>
    public sealed class ProductPrintConfigurationService : IProductPrintConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IProductPrintConfigComposer _composer;

        public ProductPrintConfigurationService(
            IUnitOfWork unitOfWork, ICurrentTenant currentTenant, IProductPrintConfigComposer composer)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
            _composer = composer;
        }

        /// <inheritdoc />
        public async Task<Result<ProductPrintConfigResponse>> GetForProductAsync(
            long productId, CancellationToken cancellationToken = default)
        {
            (Product? product, Error? error) = await LoadScopedProductAsync(productId, cancellationToken);
            if (error is not null) return Result.Failure<ProductPrintConfigResponse>(error);

            return await BuildResponseAsync(product!, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result<ProductPrintConfigResponse>> UpdateForProductAsync(
            long productId, UpdateProductPrintConfigRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<ProductPrintConfigResponse>(PrintingErrors.ProductPrintConfigOnlySystemAdmin());
            }

            (Product? product, Error? error) = await LoadScopedProductAsync(productId, cancellationToken);
            if (error is not null) return Result.Failure<ProductPrintConfigResponse>(error);

            Result<ValidatedProductPrintConfig> validation = await _composer.ValidateAsync(
                product!.TenantId, request.UsingPrinterType, request.Matica, request.Evolis, cancellationToken);
            if (validation.IsFailure)
            {
                return Result.Failure<ProductPrintConfigResponse>(validation.Error);
            }

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Keeps the product's own record of its printer family in sync with whichever
                // configuration this call actually staged — otherwise a later read would see a
                // product still claiming its old family while its configuration row says
                // otherwise (decision Q-08: switching is expressed exactly this way).
                if (product.UsingPrinterType != request.UsingPrinterType)
                {
                    product.UsingPrinterType = request.UsingPrinterType;
                    _unitOfWork.Products.Update(product);
                }

                Result replaceResult = await _composer.ReplaceForProductAsync(
                    product.TenantId, product.Id, validation.Value, cancellationToken);
                if (replaceResult.IsFailure)
                {
                    return Result.Failure<ProductPrintConfigResponse>(replaceResult.Error);
                }

                // Built from the in-memory validated entities, not a fresh query: SaveChanges has
                // not run yet at this point (ExecuteInTransactionAsync calls it after this
                // delegate returns), so an AsNoTracking query here — like BuildResponseAsync
                // below uses for a plain read — would hit the database directly and see either
                // the pre-update values or nothing at all for a brand-new row.
                ProductPrintConfigResponse response =
                    await MapValidatedResponseAsync(product.Id, validation.Value, cancellationToken);
                return Result.Success(response);
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result<ProductWithPrintConfigResponse>> GetProductWithConfigAsync(
            long productId, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<ProductWithPrintConfigResponse>(PrintingErrors.ProductPrintConfigOnlySystemAdmin());
            }

            // Admin-only, so no tenant scoping to resolve — matches every other admin-only read
            // in this module (e.g. PrinterConfigurationService reads bypass scope for an admin
            // the same way).
            Product? product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(productId, cancellationToken);
            if (product is null)
            {
                return Result.Failure<ProductWithPrintConfigResponse>(ProductErrors.NotFound(productId));
            }

            ProductResponse productResponse = MapProduct(product);
            ProductPrintConfigResponse? configResponse = await TryBuildConfigResponseAsync(product, cancellationToken);

            return new ProductWithPrintConfigResponse(productResponse, configResponse);
        }

        private async Task<ProductPrintConfigResponse> MapValidatedResponseAsync(
            long productId, ValidatedProductPrintConfig validated, CancellationToken cancellationToken)
        {
            if (validated.Matica is not null)
            {
                var maticaResponse = new MaticaPrintConfigResponse(
                    validated.Matica.Cpi, validated.Matica.FontSize,
                    validated.Matica.OffsetX, validated.Matica.OffsetY, validated.Matica.ImageId);

                return new ProductPrintConfigResponse(productId, validated.UsingPrinterType, maticaResponse, null);
            }

            EvolisProductPrintConfiguration evolis = validated.Evolis!;

            // RibbonTypes is untouched by this transaction — a reference table, not something
            // this flow inserts or updates — so querying it mid-transaction is safe and current.
            RibbonType? ribbonType = await _unitOfWork.RibbonTypes.GetByIdAsync(evolis.RibbonTypeId, cancellationToken);

            var evolisResponse = new EvolisPrintConfigResponse(
                evolis.RibbonTypeId,
                ribbonType?.Name ?? string.Empty,
                evolis.PrintWay,
                evolis.X,
                evolis.Y,
                evolis.PrintedFace,
                evolis.FontFamily,
                evolis.FontSize,
                evolis.PrintColor,
                evolis.BackgroundColor,
                evolis.FontStyle,
                evolis.ImageId);

            return new ProductPrintConfigResponse(productId, validated.UsingPrinterType, null, evolisResponse);
        }

        // Queries the database directly (AsNoTracking) — correct for a genuine read
        // (GetForProductAsync), but must never be called from inside an uncommitted
        // ExecuteInTransactionAsync delegate: SaveChanges hasn't run yet at that point, so this
        // would see stale or missing data. UpdateForProductAsync uses
        // MapValidatedResponseAsync instead, which builds the response from the in-memory
        // entities it just staged rather than re-querying.
        private async Task<Result<ProductPrintConfigResponse>> BuildResponseAsync(
            Product product, CancellationToken cancellationToken)
        {
            ProductPrintConfigResponse? response = await TryBuildConfigResponseAsync(product, cancellationToken);
            return response is null
                ? Result.Failure<ProductPrintConfigResponse>(PrintingErrors.ProductPrintConfigNotFound(product.Id))
                : response;
        }

        // Shared by BuildResponseAsync (fails when no configuration exists — the sub-resource GET
        // expects one) and GetProductWithConfigAsync (tolerates none — the admin overview should
        // work at any point in a product's lifecycle, including before a configuration exists).
        private async Task<ProductPrintConfigResponse?> TryBuildConfigResponseAsync(
            Product product, CancellationToken cancellationToken)
        {
            if (product.UsingPrinterType == UsingPrinterType.Matica)
            {
                MaticaProductPrintConfiguration? matica = await _unitOfWork.MaticaProductPrintConfigs.GetByProductIdAsync(
                    product.TenantId, product.Id, cancellationToken);
                if (matica is null)
                {
                    return null;
                }

                var maticaResponse = new MaticaPrintConfigResponse(
                    matica.Cpi, matica.FontSize, matica.OffsetX, matica.OffsetY, matica.ImageId);

                return new ProductPrintConfigResponse(product.Id, product.UsingPrinterType, maticaResponse, null);
            }

            EvolisProductPrintConfiguration? evolis = await _unitOfWork.EvolisProductPrintConfigs.GetByProductIdAsync(
                product.TenantId, product.Id, cancellationToken);
            if (evolis is null)
            {
                return null;
            }

            RibbonType? ribbonType = await _unitOfWork.RibbonTypes.GetByIdAsync(evolis.RibbonTypeId, cancellationToken);

            var evolisResponse = new EvolisPrintConfigResponse(
                evolis.RibbonTypeId,
                ribbonType?.Name ?? string.Empty,
                evolis.PrintWay,
                evolis.X,
                evolis.Y,
                evolis.PrintedFace,
                evolis.FontFamily,
                evolis.FontSize,
                evolis.PrintColor,
                evolis.BackgroundColor,
                evolis.FontStyle,
                evolis.ImageId);

            return new ProductPrintConfigResponse(product.Id, product.UsingPrinterType, null, evolisResponse);
        }

        // Duplicates ProductService.Map's single-line projection rather than depending on
        // ProductService directly — services in this codebase are siblings that talk to
        // repositories, not to each other, and this mapping is trivial enough that the
        // duplication carries negligible drift risk.
        private static ProductResponse MapProduct(Product p) => new(
            p.Id, p.TenantId, p.Name, p.ActivationStatus, p.LowProductThreshold,
            p.ProductTransactionWay, p.UsingPrinterType, p.IsDeleted, p.CreatedAt, p.UpdatedAt, p.DeletedAt);

        // Loads a product and enforces caller scope: a tenant caller may only touch its own
        // tenant's products, and a missing/out-of-scope product both return NotFound (no
        // existence leak) — matches ProductService.LoadScopedAsync exactly.
        private async Task<(Product? Product, Error? Error)> LoadScopedProductAsync(
            long productId, CancellationToken cancellationToken)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return (null, scopeError);

            Product? product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(productId, cancellationToken);
            if (product is null) return (null, ProductErrors.NotFound(productId));
            if (scope is long s && product.TenantId != s) return (null, ProductErrors.NotFound(productId));
            return (product, null);
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = PrintingErrors.ProductPrintConfigActorNotResolved();
            return null;
        }
    }
}
