using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.ProductItems;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Product-item service (API Spec §4.7). Reads are scoped as for products; the update recomputes
    /// the branch stock aggregate in the same transaction (ERD §3.1 invariant). Also owns the two
    /// Matica Print Flow backend calls (<see cref="ResolveForPrintAsync"/>/
    /// <see cref="RecordPrintResultAsync"/>) — same resource, same stock-recompute discipline as
    /// <see cref="UpdateAsync"/>, so they live here rather than in a separate service.
    /// </summary>
    public sealed class ProductItemService : IProductItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IPanFingerprintGenerator _panFingerprintGenerator;

        public ProductItemService(
            IUnitOfWork unitOfWork, ICurrentTenant currentTenant, IPanFingerprintGenerator panFingerprintGenerator)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
            _panFingerprintGenerator = panFingerprintGenerator;
        }

        public async Task<Result<PaginatedResponse<ProductItemResponse>>> GetAllAsync(
            ProductItemListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<ProductItemResponse>>(error);

            (IReadOnlyList<ProductItem> items, int total) =
                await _unitOfWork.ProductItems.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<ProductItemResponse> data = items.Select(Map).ToList();
            return PaginatedResponse<ProductItemResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<ProductItemResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return Result.Failure<ProductItemResponse>(scopeError);

            ProductItem? item = await _unitOfWork.ProductItems.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (item is null) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));
            if (scope is long s && item.TenantId != s) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));

            return Map(item);
        }

        public async Task<Result<ProductItemResponse>> UpdateAsync(
            long id, UpdateProductItemRequest request, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return Result.Failure<ProductItemResponse>(scopeError);

            ProductItem? item = await _unitOfWork.ProductItems.GetForUpdateAsync(id, cancellationToken);
            if (item is null) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));
            if (scope is long s && item.TenantId != s) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(id));

            // Transactions §4.10 (T0). Three guards, in order of terminality.
            //
            // Disposed is terminal: the card has left inventory and its quantity is in no Stock
            // column, so there is nothing coherent an edit could do.
            if (item.Status == CardStatus.Disposed)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.Disposed(id));

            // Disposal is not a status edit. It requires a mandatory reason and a disposing branch
            // for the CardDisposals record, neither of which this payload carries.
            if (request.Status == CardStatus.Disposed)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.DisposeNotAllowedHere());

            // A null branch means the card is in transit or unassigned: its quantity is committed
            // to some branch's HoldQuantity under a transfer this module knows nothing about, and
            // there is no Stock row keyed on a null branch for the delta below to land on. Without
            // this guard ApplyAvailableDeltaAsync would dereference a null BranchID.
            if (item.BranchID is not long branchId)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.InTransit(id));

            CardStatus previousStatus = item.Status;

            item.Status = request.Status;
            item.CardHolderName = request.HolderName;
            item.Notes = request.Notes;
            _unitOfWork.ProductItems.Update(item);

            // Stock recompute: only the Available boundary matters. Hold is transaction-owned and
            // is never touched here (confirmed decision).
            int delta = AvailableDelta(previousStatus, request.Status);
            if (delta != 0)
            {
                Error? stockError = await ApplyAvailableDeltaAsync(item, branchId, delta, cancellationToken);
                if (stockError is not null) return Result.Failure<ProductItemResponse>(stockError);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);   // one transaction; Stock.RowVersion guards concurrency
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<ProductItemResponse>(StockErrors.ConcurrencyConflict());
            }

            return Map(item);
        }

        /// <inheritdoc />
        public async Task<Result<ResolveForPrintResponse>> ResolveForPrintAsync(
            ResolveForPrintRequest request, CancellationToken cancellationToken = default)
        {
            // The Print Agent token always carries a tenantId claim (PrintAgentTokenGenerator);
            // a system-admin caller can never reach this endpoint at all (PrintAgentOnly policy),
            // but this mirrors every other service's own scope resolution rather than assume that.
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return Result.Failure<ResolveForPrintResponse>(scopeError);
            if (scope is not long tenantId) return Result.Failure<ResolveForPrintResponse>(AuthErrors.ActorNotResolved());

            // Raw PAN lives only in this local scope: normalized, fingerprinted, then never
            // referenced again — never assigned to a field, logged, or returned.
            string normalizedPan = BatchFileFormat.NormalizePan(request.Pan);
            if (!BatchFileFormat.IsValidPan(normalizedPan))
            {
                return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.InvalidPan());
            }

            byte[] fingerprint = _panFingerprintGenerator.Fingerprint(tenantId, normalizedPan);
            IReadOnlyDictionary<string, ProductItem> found = await _unitOfWork.ProductItems
                .GetExistingByFingerprintsAsync(tenantId, new[] { fingerprint }, cancellationToken);

            if (!found.TryGetValue(Convert.ToHexString(fingerprint), out ProductItem? item) || item is null)
            {
                return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.NotFoundForPrint());
            }

            if (item.ProductId != request.ProductId)
            {
                // Same outcome as "no match at all" — ProductItemErrors.NotFoundForPrint() is
                // deliberately one generic code; see its doc comment for why splitting this into
                // "wrong product" vs "not found" would leak more than a Print Agent token holder
                // should be able to tell apart.
                return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.NotFoundForPrint());
            }

            Product? product = await _unitOfWork.Products.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null)
            {
                return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.NotFoundForPrint());
            }

            if (product.ProductTransactionWay == ProductTransactionWay.Known)
            {
                // Known-way: the card's own recorded branch and status are the source of truth,
                // same invariant CardStatus.Available documents ("never valid while branch is null").
                if (item.BranchID != request.BranchId || item.Status != CardStatus.Available)
                {
                    return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.NotFoundForPrint());
                }
            }
            else
            {
                // Unknown-way: an unassigned-pool card sits with a null branch and
                // CardStatus.Available (set at ingestion by BatchUploadService) - OnHold is
                // reserved for a card actually moving under an in-flight transfer
                // (TransferComposer), not for one merely awaiting its first print. Its
                // availability is already counted in the branch's Stock.AvailableQuantity
                // aggregate, never in the item's own status. No FIFO selection here — the caller
                // already identifies one specific physical card by its fingerprint; FIFO only
                // applies when a caller supplies a bare quantity.
                if (item.BranchID is not null || item.Status != CardStatus.Available)
                {
                    return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.NotFoundForPrint());
                }

                Stock? stock = await _unitOfWork.Stocks.GetForUpdateAsync(
                    tenantId, request.BranchId, item.ProductId, cancellationToken);
                if (stock is null || stock.AvailableQuantity <= 0)
                {
                    return Result.Failure<ResolveForPrintResponse>(
                        StockErrors.InsufficientAvailable(request.BranchId, item.ProductId));
                }
            }

            return Result.Success(new ResolveForPrintResponse(item.ID, item.MaskedPan, item.CardHolderName));
        }

        /// <inheritdoc />
        public async Task<Result<ProductItemResponse>> RecordPrintResultAsync(
            long productItemId, RecordPrintResultRequest request, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return Result.Failure<ProductItemResponse>(scopeError);

            ProductItem? item = await _unitOfWork.ProductItems.GetForUpdateAsync(productItemId, cancellationToken);
            if (item is null) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(productItemId));
            if (scope is long s && item.TenantId != s)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFound(productItemId));

            if (item.Status == CardStatus.Disposed)
                return Result.Failure<ProductItemResponse>(ProductItemErrors.Disposed(productItemId));

            CardStatus targetStatus = request.Success ? CardStatus.SuccessPrinted : CardStatus.FailedPrinting;

            // Lightweight idempotency (deliberately not a persisted idempotency-key table, per the
            // agreed plan): a retried call that already landed — same branch, already at the
            // requested target status — is a no-op success rather than re-applying the stock delta.
            // This does not detect a retry that disagrees with the first attempt's own outcome
            // (e.g. a second call for the same attempt claiming the opposite result); that residual
            // gap was accepted explicitly rather than building a stronger, persisted check for it.
            if (item.BranchID == request.BranchId && item.Status == targetStatus)
            {
                return Map(item);
            }

            Product? product = await _unitOfWork.Products.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null) return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFoundForPrint());

            if (product.ProductTransactionWay == ProductTransactionWay.Known)
            {
                // Same shape as UpdateAsync's own guards, reusing the same private helpers below —
                // this branch is nothing new, just reached from a different entry point.
                if (item.BranchID != request.BranchId)
                    return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFoundForPrint());
                if (item.Status != CardStatus.Available)
                    return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFoundForPrint());

                CardStatus previousStatus = item.Status;
                item.Status = targetStatus;
                item.CardHolderName = request.HolderName ?? item.CardHolderName;
                _unitOfWork.ProductItems.Update(item);

                // Confirmed already correct for both outcomes without any new logic: Success and
                // FailedPrinting both leave Available (delta -1) — a spoiled card is exactly as
                // unissuable as a successfully printed one.
                int delta = AvailableDelta(previousStatus, targetStatus);
                if (delta != 0)
                {
                    Error? stockError = await ApplyAvailableDeltaAsync(item, request.BranchId, delta, cancellationToken);
                    if (stockError is not null) return Result.Failure<ProductItemResponse>(stockError);
                }
            }
            else
            {
                // Unknown-way: UpdateAsync's own InTransit guard refuses any item with a null
                // branch, which is exactly the state every Unknown-way card sits in right up until
                // this moment — so that guard, and AvailableDelta's before/after comparison (which
                // never fires, since this item was never itself Available via that code path),
                // cannot be reused here. This card's availability was already counted in the
                // branch's Stock aggregate rather than in its own status, so printing it assigns
                // the branch for the first time and decrements that aggregate directly instead.
                // CardStatus.Available, not OnHold - OnHold is reserved for a card actually moving
                // under an in-flight transfer, not one merely awaiting its first print.
                if (item.BranchID is not null || item.Status != CardStatus.Available)
                {
                    return Result.Failure<ProductItemResponse>(ProductItemErrors.NotFoundForPrint());
                }

                Stock? stock = await _unitOfWork.Stocks.GetForUpdateAsync(
                    item.TenantId, request.BranchId, item.ProductId, cancellationToken);
                if (stock is null || stock.AvailableQuantity <= 0)
                {
                    return Result.Failure<ProductItemResponse>(
                        StockErrors.InsufficientAvailable(request.BranchId, item.ProductId));
                }

                item.BranchID = request.BranchId;
                item.Status = targetStatus;
                item.CardHolderName = request.HolderName ?? item.CardHolderName;
                _unitOfWork.ProductItems.Update(item);

                stock.AvailableQuantity -= 1;
                stock.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Stocks.Update(stock);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);   // one transaction; Stock.RowVersion guards concurrency
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<ProductItemResponse>(StockErrors.ConcurrencyConflict());
            }

            return Map(item);
        }

        // +1 when the item becomes Available, -1 when it leaves Available, 0 otherwise.
        private static int AvailableDelta(CardStatus from, CardStatus to)
        {
            int before = from == CardStatus.Available ? 1 : 0;
            int after = to == CardStatus.Available ? 1 : 0;
            return after - before;
        }

        // Applies the signed delta to the branch stock row, creating the row on a positive delta if
        // it is missing. Returns an Error (not thrown) on an inconsistent state.
        //
        // branchId is passed in rather than read off the item because ProductItem.BranchID is
        // nullable (Transactions §4.10, Q4). The caller has already established it is non-null;
        // taking it as a plain long keeps that guarantee visible in the signature instead of
        // relying on a null-forgiving operator here.
        private async Task<Error?> ApplyAvailableDeltaAsync(
            ProductItem item, long branchId, int delta, CancellationToken cancellationToken)
        {
            Stock? stock = await _unitOfWork.Stocks.GetForUpdateAsync(
                item.TenantId, branchId, item.ProductId, cancellationToken);

            if (stock is null)
            {
                if (delta < 0) return StockErrors.RowNotFound(branchId, item.ProductId);

                stock = new Stock
                {
                    TenantId = item.TenantId,
                    BranchId = branchId,
                    ProductId = item.ProductId,
                    AvailableQuantity = 0,
                    HoldQuantity = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Stocks.AddAsync(stock, cancellationToken);
            }

            int updated = stock.AvailableQuantity + delta;
            if (updated < 0) return StockErrors.InsufficientAvailable(branchId, item.ProductId);

            stock.AvailableQuantity = updated;
            stock.UpdatedAt = DateTime.UtcNow;      // Stock's own UpdatedAt (base one is shadowed)
            _unitOfWork.Stocks.Update(stock);
            return null;
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

        private static ProductItemResponse Map(ProductItem x) => new(
            x.ID,
            x.TenantId,
            x.MaskedPan,
            x.ProductId,
            x.Product?.Name ?? string.Empty,
            x.BranchID,
            x.BatchId,
            x.Status,
            x.CardHolderName,
            x.Notes,
            x.IsDeleted,
            x.CreatedAt,
            x.UpdatedAt);
    }
}