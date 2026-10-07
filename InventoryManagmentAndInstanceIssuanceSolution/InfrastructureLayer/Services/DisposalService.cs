using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Disposals;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Standalone card disposal (API §4.10, Addendum A §2.4) — writing off cards that are sitting
    /// at a branch (<c>Status = Available</c>, a real <c>BranchID</c>) outside of any transfer.
    /// Disposal that happens while settling a transfer is <see cref="TransferService"/>'s
    /// responsibility, not this class's — those cards sit at no branch
    /// (<c>BranchID IS NULL</c>) and their write-off is one line in a larger settlement, not a
    /// standalone act.
    /// </summary>
    public sealed class DisposalService : IDisposalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IAuditLogger _auditLogger;

        public DisposalService(IUnitOfWork unitOfWork, ICurrentTenant currentTenant, IAuditLogger auditLogger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<Result<PaginatedResponse<DisposalListItemResponse>>> GetAllAsync(
            DisposalListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveReadScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<DisposalListItemResponse>>(error);

            (IReadOnlyList<CardDisposal> items, int total) =
                await _unitOfWork.CardDisposals.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<DisposalListItemResponse> data = items.Select(MapListItem).ToList();
            return PaginatedResponse<DisposalListItemResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<DisposalDetailResponse>> GetByIdAsync(
            long id, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveReadScope(out Error? error);
            if (error is not null) return Result.Failure<DisposalDetailResponse>(error);

            CardDisposal? disposal = await _unitOfWork.CardDisposals.GetDetailAsync(id, scope, cancellationToken);
            if (disposal is null) return Result.Failure<DisposalDetailResponse>(DisposalErrors.NotFound(id));

            return MapDetail(disposal);
        }

        public async Task<Result<DisposalDetailResponse>> CreateAsync(
            DisposeCardsRequest request, CancellationToken cancellationToken = default)
        {
            // Never permitted, no exceptions (unlike TransferService's read side, there is no
            // read-only admin path here at all — disposal has none).
            if (_currentTenant.IsSystemAdmin)
                return Result.Failure<DisposalDetailResponse>(DisposalErrors.SystemAdminNotAllowed());
            if (_currentTenant.TenantId is not long tenantId)
                return Result.Failure<DisposalDetailResponse>(DisposalErrors.ActorNotResolved());

            string reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0) return Result.Failure<DisposalDetailResponse>(DisposalErrors.ReasonRequired());
            if (reason.Length > 500) return Result.Failure<DisposalDetailResponse>(DisposalErrors.ReasonTooLong(500));

            bool hasIds = request.ProductItemIds is { Count: > 0 };
            bool hasQuantities = request.Items is { Count: > 0 };
            if (hasIds == hasQuantities)
            {
                return Result.Failure<DisposalDetailResponse>(
                    hasIds ? DisposalErrors.SelectionAmbiguous() : DisposalErrors.NothingToDispose());
            }

            List<long>? cardIds = null;
            if (hasIds)
            {
                cardIds = request.ProductItemIds!.ToList();
                var duplicate = cardIds.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
                if (duplicate is not null)
                    return Result.Failure<DisposalDetailResponse>(DisposalErrors.DuplicateItem(duplicate.Key));
            }
            else
            {
                var duplicateProduct = request.Items!.GroupBy(l => l.ProductId).FirstOrDefault(g => g.Count() > 1);
                if (duplicateProduct is not null)
                    return Result.Failure<DisposalDetailResponse>(DisposalErrors.DuplicateProduct(duplicateProduct.Key));

                foreach (DisposeCardsLine line in request.Items!)
                {
                    if (line.Quantity <= 0)
                        return Result.Failure<DisposalDetailResponse>(DisposalErrors.InvalidQuantity(line.ProductId));
                }
            }

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != tenantId)
                return Result.Failure<DisposalDetailResponse>(DisposalErrors.BranchNotFound(request.BranchId));
            if (branch.IsDeleted)
                return Result.Failure<DisposalDetailResponse>(DisposalErrors.BranchDeleted(request.BranchId));
            // IsActive is deliberately not checked: writing off damaged stock at a branch that is
            // being wound down is exactly the scenario this endpoint exists for (mirrors
            // TransferErrors.TargetBranchInactive's reasoning, applied the other way — "inactive"
            // means "accepts nothing new," not "frozen").

            // disposal, and the running per-product stock deltas, are declared here — outside the
            // transaction lambda — for the same reason as TransferService.SettleAsync: disposal.Id
            // is 0 until SaveChanges runs, which happens after the lambda returns.
            CardDisposal? disposal = null;

            Result transactionResult;
            try
            {
                transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    disposal = new CardDisposal
                    {
                        TenantId = tenantId,
                        BranchId = branch.Id,
                        CardTransferId = null,
                        DisposedByTenantId = tenantId,
                        DisposedAt = DateTime.UtcNow,
                        Reason = reason,
                    };

                    var stockDeltas = new Dictionary<long, int>();   // productId -> Available to subtract

                    if (hasIds)
                    {
                        IReadOnlyDictionary<long, ProductItem> found =
                            await _unitOfWork.ProductItems.GetManyForUpdateAsync(tenantId, cardIds!, cancellationToken);

                        foreach (long cardId in cardIds!)
                        {
                            if (!found.TryGetValue(cardId, out ProductItem? card))
                                return Result.Failure(DisposalErrors.CardNotFound(cardId));

                            Result cardCheck = ValidateStandaloneCard(card, branch.Id);
                            if (cardCheck.IsFailure) return cardCheck;

                            bool wasAvailable = card.Status == CardStatus.Available;
                            card.Status = CardStatus.Disposed;
                            // BranchID stays as-is — already == branch.Id (final resting location).
                            disposal.Items.Add(new CardDisposalItem { TenantId = tenantId, ProductItemId = card.ID });

                            if (wasAvailable)
                                stockDeltas[card.ProductId] = stockDeltas.GetValueOrDefault(card.ProductId) + 1;
                        }
                    }
                    else
                    {
                        foreach (DisposeCardsLine line in request.Items!)
                        {
                            IReadOnlyList<ProductItem> picked = await _unitOfWork.ProductItems.GetAvailableForUpdateAsync(
                                tenantId, branch.Id, line.ProductId, line.Quantity, cancellationToken);

                            if (picked.Count < line.Quantity)
                                return Result.Failure(DisposalErrors.InsufficientAvailable(branch.Id, line.ProductId));

                            foreach (ProductItem card in picked)
                            {
                                card.Status = CardStatus.Disposed;
                                disposal.Items.Add(new CardDisposalItem { TenantId = tenantId, ProductItemId = card.ID });
                            }

                            // GetAvailableForUpdateAsync only ever returns Available-status cards,
                            // so every picked card counted toward Stock.AvailableQuantity.
                            stockDeltas[line.ProductId] = stockDeltas.GetValueOrDefault(line.ProductId) + picked.Count;
                        }
                    }

                    foreach (KeyValuePair<long, int> delta in stockDeltas)
                    {
                        if (delta.Value == 0) continue;

                        Stock stock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                            tenantId, branch.Id, delta.Key, cancellationToken);

                        int updated = stock.AvailableQuantity - delta.Value;
                        if (updated < 0)
                            return Result.Failure(StockErrors.InsufficientAvailable(branch.Id, delta.Key));

                        stock.AvailableQuantity = updated;
                        stock.UpdatedAt = DateTime.UtcNow;
                    }

                    await _unitOfWork.CardDisposals.AddAsync(disposal, cancellationToken);
                    return Result.Success();
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<DisposalDetailResponse>(StockErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException)
            {
                return Result.Failure<DisposalDetailResponse>(DisposalErrors.PersistenceConflict());
            }

            if (transactionResult.IsFailure)
                return Result.Failure<DisposalDetailResponse>(transactionResult.Error);

            long disposalId = disposal!.Id;   // trustworthy now — SaveChanges has run.

            _auditLogger.StageAction(
                tenantId, tenantId, _currentTenant.Username ?? "unknown",
                "Disposed", nameof(CardDisposal), disposalId.ToString(), reason);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            CardDisposal? detail = await _unitOfWork.CardDisposals.GetDetailAsync(disposalId, tenantId, cancellationToken);
            return MapDetail(detail!);
        }

        /// <summary>
        /// Validates a card explicitly named by id is actually disposable, standalone, at the
        /// named branch. Card identity appears in error messages only via its masked PAN.
        /// </summary>
        private static Result ValidateStandaloneCard(ProductItem card, long branchId)
        {
            if (card.Status == CardStatus.Disposed)
                return Result.Failure(DisposalErrors.AlreadyDisposed(card.MaskedPan));
            if (card.Status == CardStatus.SuccessPrinted)
                return Result.Failure(DisposalErrors.NotDisposable(card.MaskedPan));
            if (card.BranchID is null || card.Status == CardStatus.OnHold)
                return Result.Failure(DisposalErrors.CardInTransfer(card.MaskedPan));
            if (card.BranchID != branchId)
                return Result.Failure(DisposalErrors.CardNotAtBranch(card.MaskedPan));

            // Available, FailedPrinting, and Expired all reach here — every status that
            // represents inventory genuinely sitting at this branch (EC-92).
            return Result.Success();
        }

        private long? ResolveReadScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = DisposalErrors.ActorNotResolved();
            return null;
        }

        private static DisposalListItemResponse MapListItem(CardDisposal d) => new(
            d.Id, d.TenantId, d.BranchId, d.Branch.Name, d.CardTransferId, d.Reason, d.Items.Count, d.DisposedAt);

        private static DisposalDetailResponse MapDetail(CardDisposal d) => new(
            d.Id, d.TenantId, d.BranchId, d.Branch.Name, d.CardTransferId, d.DisposedByTenantId,
            d.Reason, d.DisposedAt,
            d.Items.Select(i => new DisposalItemResponse(
                i.ProductItemId, i.ProductItem.MaskedPan, i.ProductItem.ProductId, i.ProductItem.Product.Name))
                .ToList());
    }
}
