using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.BranchRequests;
using ApplicationLayer.DTOs.Transfers;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Branch stock request orchestration (API §4.9). Actor resolution mirrors
    /// <see cref="TransferService"/> exactly: a system admin reads across tenants and is
    /// rejected outright on every write (a request's <c>ActionTakenByTenantId</c> has nowhere to
    /// point for an admin token, matching <c>CardTransfer.CreatedByTenantId</c>'s own reasoning).
    /// </summary>
    public sealed class BranchRequestService : IBranchRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IAuditLogger _auditLogger;
        private readonly ITransferComposer _transferComposer;

        public BranchRequestService(
            IUnitOfWork unitOfWork, ICurrentTenant currentTenant, IAuditLogger auditLogger,
            ITransferComposer transferComposer)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _transferComposer = transferComposer ?? throw new ArgumentNullException(nameof(transferComposer));
        }

        // =====================================================================================
        //  Reads
        // =====================================================================================

        public async Task<Result<PaginatedResponse<StockRequestListItemResponse>>> GetAllAsync(
            StockRequestListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveReadScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<StockRequestListItemResponse>>(error);

            (IReadOnlyList<BranchRequest> items, int total) =
                await _unitOfWork.BranchRequests.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<StockRequestListItemResponse> data = items.Select(MapListItem).ToList();
            return PaginatedResponse<StockRequestListItemResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<StockRequestDetailResponse>> GetByIdAsync(
            long id, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveReadScope(out Error? error);
            if (error is not null) return Result.Failure<StockRequestDetailResponse>(error);

            return await LoadDetailAsync(id, scope, cancellationToken);
        }

        // =====================================================================================
        //  Create
        // =====================================================================================

        public async Task<Result<StockRequestDetailResponse>> CreateAsync(
            CreateStockRequest request, CancellationToken cancellationToken = default)
        {
            Result<long> actor = ResolveWritingTenant();
            if (actor.IsFailure) return Result.Failure<StockRequestDetailResponse>(actor.Error);
            long tenantId = actor.Value;

            Result shape = ValidateCreateShape(request);
            if (shape.IsFailure) return Result.Failure<StockRequestDetailResponse>(shape.Error);

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(
                request.RequestingBranchId, cancellationToken);
            if (branch is null || branch.TenantId != tenantId)
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.BranchNotFound(request.RequestingBranchId));
            if (branch.IsDeleted)
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.BranchDeleted(request.RequestingBranchId));
            // Decision Q-13: a request for an inactive branch can never be confirmed — the branch
            // would always be rejected as an inactive transfer target — so this fails at creation
            // rather than admitting a request that can only ever sit unconfirmable.
            if (!branch.IsActive)
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.BranchInactive(request.RequestingBranchId));

            // Load and validate every product line before writing anything. A bad line anywhere
            // fails the whole create — nothing is partially applied.
            foreach (StockRequestLine line in request.Items)
            {
                Product? product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(line.ProductId, cancellationToken);
                if (product is null || product.TenantId != tenantId || product.IsDeleted)
                    return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.ProductNotFound(line.ProductId));
            }

            // Decision Q-11 / D-08: block a request that would overlap an existing non-terminal
            // request for the same branch on any of the same products.
            IReadOnlyCollection<long> openProductIds = await _unitOfWork.BranchRequests.GetOpenProductIdsForBranchAsync(
                tenantId, request.RequestingBranchId, cancellationToken);
            foreach (StockRequestLine line in request.Items)
            {
                if (openProductIds.Contains(line.ProductId))
                    return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.DuplicateOpenRequest(line.ProductId));
            }

            var branchRequest = new BranchRequest
            {
                TenantId = tenantId,
                RequestingBranchId = request.RequestingBranchId,
                RequestDateTime = DateTime.UtcNow,
                RequestStatus = BranchRequestStatus.InProgress,
                ActionNotes = request.ActionNotes,
            };
            foreach (StockRequestLine line in request.Items)
            {
                branchRequest.Items.Add(new BranchRequestItem
                {
                    TenantId = tenantId,
                    ProductId = line.ProductId,
                    AskedQuantity = line.AskedQuantity,
                    DispatchedQuantity = 0,
                    ReceivedQuantity = 0,
                });
            }

            Result<BranchRequest> transactionResult;
            try
            {
                transactionResult = await _unitOfWork.ExecuteInTransactionAsync<BranchRequest>(async () =>
                {
                    await _unitOfWork.BranchRequests.AddAsync(branchRequest, cancellationToken);
                    return Result.Success(branchRequest);
                }, cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.PersistenceConflict());
            }

            if (transactionResult.IsFailure)
                return Result.Failure<StockRequestDetailResponse>(transactionResult.Error);

            BranchRequest committed = transactionResult.Value;

            // Audit second, after the id exists — see IAuditLogger.StageAction's doc comment for
            // why this is a deliberately separate save rather than inside the transaction above.
            _auditLogger.StageAction(
                tenantId, tenantId, _currentTenant.Username ?? "unknown",
                "Created", nameof(BranchRequest), committed.Id.ToString());
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await LoadDetailAsync(committed.Id, tenantId, cancellationToken);
        }

        // =====================================================================================
        //  Confirm
        // =====================================================================================

        public async Task<Result<ConfirmStockRequestResult>> ConfirmAsync(
            long id, ConfirmStockRequest request, CancellationToken cancellationToken = default)
        {
            Result<long> actor = ResolveWritingTenant();
            if (actor.IsFailure) return Result.Failure<ConfirmStockRequestResult>(actor.Error);
            long tenantId = actor.Value;

            if (request.Transfers is null || request.Transfers.Count == 0)
                return Result.Failure<ConfirmStockRequestResult>(BranchRequestErrors.NoTransfers());

            BranchRequest? branchRequest = await _unitOfWork.BranchRequests.GetForUpdateAsync(id, tenantId, cancellationToken);
            if (branchRequest is null) return Result.Failure<ConfirmStockRequestResult>(BranchRequestErrors.NotFound(id));

            if (branchRequest.RequestStatus is BranchRequestStatus.Fulfilled or BranchRequestStatus.Refused or BranchRequestStatus.Cancelled)
                return Result.Failure<ConfirmStockRequestResult>(BranchRequestErrors.NotOpenForConfirmation(id));

            // Validate every plan before writing anything — a bad plan anywhere fails the whole
            // confirm, exactly like TransferService.CreateAsync validating every line up front.
            var validatedPlans = new List<ValidatedTransferPlan>(request.Transfers.Count);
            foreach (ConfirmTransferPlan plan in request.Transfers)
            {
                // Pre-empts the database check CK_CardsTransferHistory_SourceNotTarget, which
                // would otherwise fire once the generated transfer is staged.
                if (plan.SourceBranchId == branchRequest.RequestingBranchId)
                    return Result.Failure<ConfirmStockRequestResult>(
                        BranchRequestErrors.SourceIsRequestingBranch(plan.SourceBranchId));

                Result<ValidatedTransferPlan> validated = await _transferComposer.ValidateAsync(
                    tenantId, plan.SourceBranchId, branchRequest.RequestingBranchId, plan.Items, plan.ActionNotes,
                    cancellationToken);
                if (validated.IsFailure) return Result.Failure<ConfirmStockRequestResult>(validated.Error);

                validatedPlans.Add(validated.Value);
            }

            var stagedTransfers = new List<CardTransfer>(validatedPlans.Count);
            string createdByUsername = _currentTenant.Username ?? "unknown";

            Result transactionResult;
            try
            {
                transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    foreach (ValidatedTransferPlan plan in validatedPlans)
                    {
                        Result<CardTransfer> staged = await _transferComposer.StageAsync(
                            tenantId, plan, branchRequestId: branchRequest.Id, createdByUsername, cancellationToken);
                        if (staged.IsFailure) return Result.Failure(staged.Error);

                        CardTransfer transfer = staged.Value;
                        stagedTransfers.Add(transfer);

                        foreach (CardTransferProduct line in transfer.Products)
                        {
                            // D-05: a product confirmed that is not one of the request's own
                            // lines is never written back onto the request — it appears only on
                            // the generated transfer and, at read time, in UnrequestedProducts.
                            BranchRequestItem? item = branchRequest.Items.FirstOrDefault(i => i.ProductId == line.ProductId);
                            if (item is null) continue;

                            item.CreditDispatched(line.TransactedQuantity);

                            // Unknown-way Maker-Checker workflow: every line staged by the
                            // composer is pending now (RealQuantityReceived null), Known or
                            // Unknown alike — this check only ever fires for a line the composer
                            // settled inline, which no longer happens for either way. Left in
                            // place (rather than removed) as the single correct credit point for
                            // any future line shape that *does* settle inline; today,
                            // ReceivedQuantity for every line generated here is credited later by
                            // BranchRequestFulfilment.ApplyReceiptAsync when its transfer settles.
                            if (line.RealQuantityReceived is int received)
                                item.CreditReceived(received);
                        }
                    }

                    branchRequest.ActionTakenByTenantId = tenantId;
                    branchRequest.ActionTakenAt = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(request.ActionNotes)) branchRequest.ActionNotes = request.ActionNotes;

                    // Called once, after every plan and every line has been processed. No
                    // special-casing needed for an all-Unknown-way confirm jumping straight to
                    // Fulfilled in one call — this is a pure function of the counters (D-03) and
                    // is correct by construction either way.
                    branchRequest.RecomputeStatus();

                    return Result.Success();
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<ConfirmStockRequestResult>(BranchRequestErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException)
            {
                return Result.Failure<ConfirmStockRequestResult>(BranchRequestErrors.PersistenceConflict());
            }

            if (transactionResult.IsFailure)
                return Result.Failure<ConfirmStockRequestResult>(transactionResult.Error);

            // Audit second, after every id exists — one row for the request, one per generated
            // transfer, matching TransferService.CreateAsync's own deliberate separate-save.
            _auditLogger.StageAction(
                tenantId, tenantId, _currentTenant.Username ?? "unknown",
                "Confirmed", nameof(BranchRequest), branchRequest.Id.ToString());
            foreach (CardTransfer transfer in stagedTransfers)
            {
                _auditLogger.StageAction(
                    tenantId, tenantId, _currentTenant.Username ?? "unknown",
                    "Transfer", nameof(CardTransfer), transfer.Id.ToString());
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            Result<StockRequestDetailResponse> detail = await LoadDetailAsync(branchRequest.Id, tenantId, cancellationToken);
            if (detail.IsFailure) return Result.Failure<ConfirmStockRequestResult>(detail.Error);

            var transferDetails = new List<TransferDetailResponse>(stagedTransfers.Count);
            foreach (CardTransfer transfer in stagedTransfers)
            {
                CardTransfer? full = await _unitOfWork.CardTransfers.GetDetailAsync(transfer.Id, tenantId, cancellationToken);
                transferDetails.Add(TransferService.MapDetail(full!));
            }

            return new ConfirmStockRequestResult(detail.Value, transferDetails);
        }

        // =====================================================================================
        //  Refuse / Cancel
        // =====================================================================================

        public Task<Result<StockRequestDetailResponse>> RefuseAsync(
            long id, RefuseStockRequest request, CancellationToken cancellationToken = default)
            => CloseAsync(id, request.ActionNotes, BranchRequestStatus.Refused, "Refused", cancellationToken);

        public Task<Result<StockRequestDetailResponse>> CancelAsync(
            long id, CancelStockRequest request, CancellationToken cancellationToken = default)
            => CloseAsync(id, request.ActionNotes, BranchRequestStatus.Cancelled, "Cancelled", cancellationToken);

        /// <summary>
        /// Shared body for <see cref="RefuseAsync"/> and <see cref="CancelAsync"/> — identical
        /// guard, mutation, and audit shape; only the target status and audit action differ
        /// (decision D-06).
        /// </summary>
        private async Task<Result<StockRequestDetailResponse>> CloseAsync(
            long id, string? actionNotes, BranchRequestStatus targetStatus, string auditAction,
            CancellationToken cancellationToken)
        {
            Result<long> actor = ResolveWritingTenant();
            if (actor.IsFailure) return Result.Failure<StockRequestDetailResponse>(actor.Error);
            long tenantId = actor.Value;

            if (actionNotes is { Length: > 500 })
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.ActionNotesTooLong(500));

            BranchRequest? branchRequest = await _unitOfWork.BranchRequests.GetForUpdateAsync(id, tenantId, cancellationToken);
            if (branchRequest is null) return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.NotFound(id));

            // Decision D-06: allowed only from InProgress or PartiallyConfirmed. Once anything has
            // been received the request cannot be walked back; already-dispatched transfers are
            // left to complete their own §4.10 lifecycle regardless of this closure.
            if (branchRequest.RequestStatus is not (BranchRequestStatus.InProgress or BranchRequestStatus.PartiallyConfirmed))
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.NotOpenForClosure(id));

            // RecomputeStatus() is deliberately not called here — a terminal status is assigned
            // directly, exactly as BranchRequest.RecomputeStatus's own doc comment describes.
            branchRequest.RequestStatus = targetStatus;
            branchRequest.ActionTakenByTenantId = tenantId;
            branchRequest.ActionTakenAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(actionNotes)) branchRequest.ActionNotes = actionNotes;

            _auditLogger.StageAction(
                tenantId, tenantId, _currentTenant.Username ?? "unknown",
                auditAction, nameof(BranchRequest), branchRequest.Id.ToString());

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException)
            {
                return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.PersistenceConflict());
            }

            return await LoadDetailAsync(branchRequest.Id, tenantId, cancellationToken);
        }

        // =====================================================================================
        //  Validation helpers
        // =====================================================================================

        private static Result ValidateCreateShape(CreateStockRequest request)
        {
            if (request.Items is null || request.Items.Count == 0)
                return Result.Failure(BranchRequestErrors.NoItems());

            if (request.ActionNotes is { Length: > 500 })
                return Result.Failure(BranchRequestErrors.ActionNotesTooLong(500));

            var seenProducts = new HashSet<long>();
            foreach (StockRequestLine line in request.Items)
            {
                if (!seenProducts.Add(line.ProductId))
                    return Result.Failure(BranchRequestErrors.DuplicateProduct(line.ProductId));
                if (line.AskedQuantity <= 0)
                    return Result.Failure(BranchRequestErrors.InvalidQuantity(line.ProductId));
            }

            // Decision Q-06: no volume caps — unlike TransferService.ValidateCreateShape, there is
            // deliberately no per-request card-count limit here.
            return Result.Success();
        }

        // null scope => system admin (read-only, §11); otherwise the tenant caller's id.
        private long? ResolveReadScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = BranchRequestErrors.ActorNotResolved();
            return null;
        }

        // Every write in this service rejects a system admin outright (§11): a request's
        // ActionTakenByTenantId has no admin id to point to, and admin access here is read-only.
        private Result<long> ResolveWritingTenant()
        {
            if (_currentTenant.IsSystemAdmin) return Result.Failure<long>(BranchRequestErrors.SystemAdminNotAllowed());
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            return Result.Failure<long>(BranchRequestErrors.ActorNotResolved());
        }

        // =====================================================================================
        //  Mapping
        // =====================================================================================

        private static StockRequestListItemResponse MapListItem(BranchRequest r) => new(
            r.Id, r.TenantId,
            r.RequestingBranchId, r.RequestingBranch?.Name ?? string.Empty,
            r.RequestStatus,
            r.Items.Count, r.Items.Sum(i => i.AskedQuantity), r.Items.Sum(i => i.ReceivedQuantity),
            r.RequestDateTime, r.ActionTakenAt);

        /// <summary>
        /// Reloads a request by id (fresh, no-tracking detail query — the same "reload after
        /// commit" pattern <c>TransferService.CreateAsync</c> uses) and maps it to the full
        /// detail DTO, including the derived <c>UnrequestedProducts</c> collection (decision
        /// D-05) computed from the request's linked transfers.
        /// </summary>
        private async Task<Result<StockRequestDetailResponse>> LoadDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken)
        {
            BranchRequest? branchRequest = await _unitOfWork.BranchRequests.GetDetailAsync(id, tenantScopeId, cancellationToken);
            if (branchRequest is null) return Result.Failure<StockRequestDetailResponse>(BranchRequestErrors.NotFound(id));

            // Bounded to the first 100 linked transfers — CardTransferRepo.GetPagedAsync's own
            // page-size ceiling, unmodified by this phase. A single request generating more than
            // 100 transfers is far outside anything §4.9 anticipates; TransferIds and
            // UnrequestedProducts would be partial in that extreme case.
            var transferFilter = new TransferListFilter(BranchRequestId: branchRequest.Id, PageSize: 100);
            (IReadOnlyList<CardTransfer> transfers, _) = await _unitOfWork.CardTransfers.GetPagedAsync(
                branchRequest.TenantId, transferFilter, cancellationToken);

            var requestedProductIds = branchRequest.Items.Select(i => i.ProductId).ToHashSet();
            var unrequestedTotals = new Dictionary<long, (int Dispatched, int Received)>();
            foreach (CardTransfer transfer in transfers)
            {
                foreach (CardTransferProduct line in transfer.Products)
                {
                    if (requestedProductIds.Contains(line.ProductId)) continue;

                    (int dispatched, int received) = unrequestedTotals.TryGetValue(line.ProductId, out var existing)
                        ? existing : (0, 0);
                    unrequestedTotals[line.ProductId] = (
                        dispatched + line.TransactedQuantity,
                        received + (line.RealQuantityReceived ?? 0));
                }
            }

            var unrequestedProducts = new List<UnrequestedProductResponse>(unrequestedTotals.Count);
            foreach ((long productId, (int dispatched, int received)) in unrequestedTotals)
            {
                Product? product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(productId, cancellationToken);
                unrequestedProducts.Add(new UnrequestedProductResponse(productId, product?.Name ?? string.Empty, dispatched, received));
            }

            IReadOnlyList<StockRequestItemResponse> items = branchRequest.Items.Select(i => new StockRequestItemResponse(
                i.ProductId, i.Product?.Name ?? string.Empty,
                i.AskedQuantity, i.DispatchedQuantity, i.ReceivedQuantity,
                Math.Max(0, i.AskedQuantity - i.ReceivedQuantity),
                i.Product?.ProductTransactionWay ?? default)).ToList();

            return new StockRequestDetailResponse(
                branchRequest.Id, branchRequest.TenantId,
                branchRequest.RequestingBranchId, branchRequest.RequestingBranch?.Name ?? string.Empty,
                branchRequest.RequestStatus, branchRequest.RequestDateTime,
                branchRequest.ActionTakenByTenantId, branchRequest.ActionTakenAt, branchRequest.ActionNotes,
                Convert.ToBase64String(branchRequest.RowVersion),
                items, unrequestedProducts, transfers.Select(t => t.Id).ToList());
        }
    }
}
