using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Products;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Product (catalog) management service (API Spec §4.6). Resolves the caller from
    /// <see cref="ICurrentTenant"/>: a tenant principal is scoped to its own tenant; a system admin
    /// bypasses scoping and may target any tenant. Business failures surface as categorized
    /// <see cref="Error"/>s.
    /// <para>
    /// Printing Module, phase 7: <see cref="CreateAsync"/> can attach a print configuration in the
    /// same call via <see cref="IProductPrintConfigComposer"/> — system-admin only (decision Q-09,
    /// confirmed) — and <see cref="SoftDeleteAsync"/>/<see cref="RestoreAsync"/> cascade to it.
    /// <see cref="UpdateAsync"/> deliberately does not touch it at all: a product's printer family
    /// can only change via <c>PUT /api/products/{id}/print-config</c> (confirmed), so this service
    /// never has two different code paths capable of changing it.
    /// </para>
    /// </summary>
    public sealed class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IProductPrintConfigComposer _composer;

        public ProductService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant, IProductPrintConfigComposer composer)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
            _composer = composer;
        }

        public async Task<Result<PaginatedResponse<ProductResponse>>> GetAllAsync(
            ProductListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<ProductResponse>>(error);

            (IReadOnlyList<Product> items, int total) =
                await _unitOfWork.Products.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<ProductResponse> data = items.Select(Map).ToList();
            return PaginatedResponse<ProductResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<ProductResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            (Product? product, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<ProductResponse>(error);
            // NOTE (stock seam): per API §4.6 the detail response should include aggregated stock
            // across branches; wire that in once the Stock aggregate (ERD §3.1) exists.
            return Map(product!);
        }

        public async Task<Result<ProductResponse>> CreateAsync(
            CreateProductRequest request, CancellationToken cancellationToken = default)
        {
            bool hasPrintConfigPayload = request.Matica is not null || request.Evolis is not null;

            // Decision Q-09, confirmed: a tenant caller may still create a plain product — only
            // attaching a print configuration in the same call is system-admin only. Checked
            // before tenant resolution since it's a pure role check, independent of it.
            if (hasPrintConfigPayload && !_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<ProductResponse>(PrintingErrors.ProductPrintConfigOnlySystemAdmin());
            }

            long targetTenantId;

            if (_currentTenant.IsSystemAdmin)
            {
                if (request.TenantId is not long requested) return Result.Failure<ProductResponse>(ProductErrors.TenantRequired());
                if (await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(requested, cancellationToken) is null)
                    return Result.Failure<ProductResponse>(ProductErrors.TargetTenantNotFound(requested));
                targetTenantId = requested;
            }
            else
            {
                if (_currentTenant.TenantId is not long callerTenant) return Result.Failure<ProductResponse>(AuthErrors.ActorNotResolved());
                targetTenantId = callerTenant;
            }

            if (await _unitOfWork.Products.NameExistsAsync(targetTenantId, request.Name, null, cancellationToken))
                return Result.Failure<ProductResponse>(ProductErrors.NameAlreadyExists(request.Name));

            // Read-only validation, matching IProductPrintConfigComposer's own contract: resolved
            // before the transaction opens, so a bad payload never creates the product either —
            // the module requirement is "single aggregate", not "product first, config maybe".
            ValidatedProductPrintConfig? validatedConfig = null;
            if (hasPrintConfigPayload)
            {
                Result<ValidatedProductPrintConfig> validation = await _composer.ValidateAsync(
                    targetTenantId, request.UsingPrinterType, request.Matica, request.Evolis, cancellationToken);
                if (validation.IsFailure)
                {
                    return Result.Failure<ProductResponse>(validation.Error);
                }

                validatedConfig = validation.Value;
            }

            // Declared here, outside the transaction lambda, for the same reason as
            // PrinterConfigurationService.CreateAsync: product.Id and product.CreatedAt are 0 /
            // default until SaveChanges runs, which happens after the lambda returns, so the
            // response must be built from this same instance after the transaction completes.
            var product = new Product
            {
                TenantId = targetTenantId,
                Name = request.Name,
                ActivationStatus = request.ActivationStatus,
                LowProductThreshold = request.LowProductThreshold,
                ProductTransactionWay = request.ProductTransactionWay,
                UsingPrinterType = request.UsingPrinterType
            };

            Result transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _unitOfWork.Products.AddAsync(product, cancellationToken);

                if (validatedConfig is not null)
                {
                    // Passes the Product entity itself, not its id: product.Id does not exist yet
                    // — it's generated by the AddAsync above, once this transaction's single
                    // SaveChanges call actually runs. StageForProductAsync sets the
                    // configuration's Product navigation so EF Core fixes up the foreign key once
                    // that id is generated (see IProductPrintConfigComposer's doc comment).
                    await _composer.StageForProductAsync(targetTenantId, product, validatedConfig, cancellationToken);
                }

                return Result.Success();
            }, cancellationToken);

            if (transactionResult.IsFailure)
            {
                return Result.Failure<ProductResponse>(transactionResult.Error);
            }

            return Result.Success(Map(product));
        }

        public async Task<Result<ProductResponse>> UpdateAsync(
            long id, UpdateProductRequest request, CancellationToken cancellationToken = default)
        {
            (Product? product, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<ProductResponse>(error);

            if (!string.Equals(product!.Name, request.Name, StringComparison.Ordinal) &&
                await _unitOfWork.Products.NameExistsAsync(product.TenantId, request.Name, id, cancellationToken))
                return Result.Failure<ProductResponse>(ProductErrors.NameAlreadyExists(request.Name));

            // Transactions §4.10 (P6): ProductTransactionWay is frozen once any card exists for the
            // product. Known and Unknown track cards differently — Known enumerates them per
            // transfer, Unknown moves quantities against a shared pool — and every transfer line
            // snapshots the value at creation time. Flipping it mid-life would leave existing cards
            // tracked one way while new transfers assume the other, with nothing in the data to
            // distinguish them afterwards. The check is skipped when the value is unchanged, so an
            // ordinary rename or threshold edit never pays for the query.
            if (product.ProductTransactionWay != request.ProductTransactionWay &&
                await _unitOfWork.ProductItems.ExistsForProductAsync(product.TenantId, id, cancellationToken))
            {
                return Result.Failure<ProductResponse>(ProductErrors.TransactionWayImmutable(id));
            }

            product.Name = request.Name;
            product.ActivationStatus = request.ActivationStatus;
            product.LowProductThreshold = request.LowProductThreshold;
            product.ProductTransactionWay = request.ProductTransactionWay;
            // UsingPrinterType is intentionally not touched here (Printing Module, phase 7,
            // confirmed): it can only change via PUT /api/products/{id}/print-config, which
            // switches it atomically together with the matching configuration row (decision
            // Q-08). UpdateProductRequest no longer carries the field at all.

            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(product);
        }

        public async Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            (Product? product, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (product!.IsDeleted) return Result.Failure(ProductErrors.AlreadyDeleted(id));

            // TODO (stock seam): per API §4.6 block deletion when the product has non-zero stock or
            // open transactions, returning a 409 (e.g. ProductErrors.HasStock). Still unconditional
            // until the Stock/Transactions modules add that check.
            //
            // API §4.9 guard (EC-R37): a product referenced by an open branch stock request line
            // cannot be deleted while that request is still open.
            if (await _unitOfWork.BranchRequests.HasOpenRequestForProductAsync(product.TenantId, id, cancellationToken))
                return Result.Failure(ProductErrors.HasOpenRequest(id));

            (long? actorId, Error? actorError) = await ResolveActorIdAsync(cancellationToken);
            if (actorError is not null) return Result.Failure(actorError);

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                product.IsDeleted = true;
                product.DeletedAt = DateTime.UtcNow;
                product.DeletedBy = actorId;
                _unitOfWork.Products.Update(product);

                // Module requirement §4: "deleting a product should remove its associated print
                // configuration." Silently no-ops if the product has no configuration row.
                await _composer.SoftDeleteForProductAsync(product.TenantId, product.Id, actorId, cancellationToken);

                return Result.Success();
            }, cancellationToken);
        }

        public async Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default)
        {
            (Product? product, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (!product!.IsDeleted) return Result.Failure(ProductErrors.NotDeleted(id));

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                product.IsDeleted = false;
                product.DeletedAt = null;
                product.DeletedBy = null;
                _unitOfWork.Products.Update(product);

                await _composer.RestoreForProductAsync(product.TenantId, product.Id, cancellationToken);

                return Result.Success();
            }, cancellationToken);
        }

        public Task<Result<ProductResponse>> ActivateAsync(long id, CancellationToken cancellationToken = default) =>
            SetActivationAsync(id, ActivationStatus.Active, cancellationToken);

        public Task<Result<ProductResponse>> DeactivateAsync(long id, CancellationToken cancellationToken = default) =>
            SetActivationAsync(id, ActivationStatus.Inactive, cancellationToken);

        private async Task<Result<ProductResponse>> SetActivationAsync(long id, ActivationStatus status, CancellationToken cancellationToken)
        {
            (Product? product, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<ProductResponse>(error);

            if (product!.ActivationStatus == status) return Map(product);   // idempotent no-op

            product.ActivationStatus = status;
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(product);
        }

        // Loads a product and enforces caller scope: a tenant caller may only touch its own tenant's
        // products, and a missing/out-of-scope product both return NotFound (no existence leak).
        private async Task<(Product? Product, Error? Error)> LoadScopedAsync(long id, CancellationToken cancellationToken)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return (null, scopeError);

            Product? product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (product is null) return (null, ProductErrors.NotFound(id));
            if (scope is long s && product.TenantId != s) return (null, ProductErrors.NotFound(id));
            return (product, null);
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = AuthErrors.ActorNotResolved();
            return null;
        }

        private async Task<(long? ActorId, Error? Error)> ResolveActorIdAsync(CancellationToken cancellationToken)
        {
            if (!_currentTenant.IsSystemAdmin)
                return (_currentTenant.TenantId, _currentTenant.TenantId is null ? AuthErrors.ActorNotResolved() : null);

            SystemAdmin? admin = await _unitOfWork.SystemAdmins.GetActiveByUsernameAsync(_currentTenant.Username!, cancellationToken);
            return admin is null ? (null, AuthErrors.ActorNotResolved()) : (admin.Id, null);
        }

        private static ProductResponse Map(Product p) => new(
            p.Id, p.TenantId, p.Name, p.ActivationStatus, p.LowProductThreshold,
            p.ProductTransactionWay, p.UsingPrinterType, p.IsDeleted, p.CreatedAt, p.UpdatedAt, p.DeletedAt);
    }
}
