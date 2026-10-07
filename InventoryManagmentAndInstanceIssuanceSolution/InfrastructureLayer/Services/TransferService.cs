using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
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
    /// Card-transfer orchestration (API §4.10).
    /// <para>
    /// <b>The settlement rule, once, precisely — everything in <see cref="SettleAsync"/> is this
    /// rule applied per product line:</b> a line ships <c>Q</c> cards. At creation, the source's
    /// <c>Available</c> drops by <c>Q</c> and its <c>Hold</c> rises by <c>Q</c> — the shipment is
    /// now "in flight," accounted for at the source alone. At settlement the caller states
    /// <c>Received (R)</c> and <c>Disposed (D)</c>; the remainder <c>Returned = Q − R − D</c> is
    /// derived, never supplied. Settlement always releases the source's <c>Hold</c> by the full
    /// <c>Q</c> — the shipment is no longer in flight regardless of how it was split — and adds
    /// <c>R</c> to the target's <c>Available</c>. If <c>Returned &gt; 0</c>, the target's
    /// <c>Hold</c> rises by that amount and a transfer running the other way (target → source)
    /// carries it: this is the auto-generated return, and its own future settlement applies the
    /// exact same rule with the branches swapped. <c>Disposed</c> touches no further stock
    /// anywhere — it already left the source's <c>Hold</c> above and never entered the target's
    /// <c>Available</c>, so writing it off has nothing further to undo.
    /// </para>
    /// <para>
    /// <b>Correction to Addendum A §2.2's stock table:</b> that note described disposal as
    /// "<c>Hold −n</c> at the disposing branch." The rule above is more precise: the hold release
    /// is always the source's, for the line's full <c>Q</c>, in one release — not a separate
    /// decrement keyed to whichever branch happens to perform the write-off. The disposing branch
    /// supplied by the caller is accountability metadata for the <c>CardDisposal</c> record; it
    /// does not correspond to a stock column of its own.
    /// </para>
    /// <para>
    /// A return transfer is never passed through <see cref="CreateAsync"/> — it is built directly
    /// inside a settlement, because the stock movement it represents (the target's <c>Hold</c>
    /// rising) has already happened as part of that same settlement. Running it through the normal
    /// create path afterwards would double-count it.
    /// </para>
    /// <para>
    /// <b>Unknown-way Maker-Checker workflow addendum:</b> an Unknown-way line follows the exact
    /// same rule above when its remainder resolves as <see cref="TransferDifferenceAction.ReturnedToSource"/>
    /// — <c>R</c> credits the target's <c>Available</c>, the remainder spawns a return transfer,
    /// nothing physical is ever touched because there is none. The one genuine deviation is
    /// <see cref="TransferDifferenceAction.KeptAtDestination"/>: the target's <c>Available</c>
    /// receives the <em>full</em> transacted quantity rather than only <c>R</c>, and no return
    /// transfer is spawned at all — the discrepancy between what was confirmed
    /// (<c>RealQuantityReceived</c>) and what was credited stays visible on the line rather than
    /// being expressed as stock in transit, because nothing is actually in transit for an
    /// Unknown-way remainder resolved this way.
    /// </para>
    /// </summary>
    public sealed class TransferService : ITransferService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly IAuditLogger _auditLogger;
        private readonly ITransferComposer _transferComposer;
        private readonly IBranchRequestFulfilment _branchRequestFulfilment;

        public TransferService(
            IUnitOfWork unitOfWork, ICurrentTenant currentTenant, IAuditLogger auditLogger,
            ITransferComposer transferComposer, IBranchRequestFulfilment branchRequestFulfilment)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _transferComposer = transferComposer ?? throw new ArgumentNullException(nameof(transferComposer));
            _branchRequestFulfilment = branchRequestFulfilment ?? throw new ArgumentNullException(nameof(branchRequestFulfilment));
        }

        // =====================================================================================
        //  Reads
        // =====================================================================================

        public async Task<Result<PaginatedResponse<TransferListItemResponse>>> GetAllAsync(
            TransferListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveReadScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<TransferListItemResponse>>(error);

            (IReadOnlyList<CardTransfer> items, int total) =
                await _unitOfWork.CardTransfers.GetPagedAsync(scope, filter, cancellationToken);

            IReadOnlyList<TransferListItemResponse> data = items.Select(MapListItem).ToList();
            return PaginatedResponse<TransferListItemResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        public async Task<Result<TransferDetailResponse>> GetByIdAsync(
            long id, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveReadScope(out Error? error);
            if (error is not null) return Result.Failure<TransferDetailResponse>(error);

            CardTransfer? transfer = await _unitOfWork.CardTransfers.GetDetailAsync(id, scope, cancellationToken);
            if (transfer is null) return Result.Failure<TransferDetailResponse>(TransferErrors.NotFound(id));

            return MapDetail(transfer);
        }

        // =====================================================================================
        //  Create
        // =====================================================================================

        public async Task<Result<TransferDetailResponse>> CreateAsync(
            CreateTransferRequest request, CancellationToken cancellationToken = default)
        {
            Result<long> actor = ResolveWritingTenant();
            if (actor.IsFailure) return Result.Failure<TransferDetailResponse>(actor.Error);
            long tenantId = actor.Value;

            Result validation = ValidateCreateShape(request);
            if (validation.IsFailure) return Result.Failure<TransferDetailResponse>(validation.Error);

            Result<ValidatedTransferPlan> planResult = await _transferComposer.ValidateAsync(
                tenantId, request.SourceBranchId, request.TargetBranchId, request.Items!, request.ActionNotes,
                cancellationToken);
            if (planResult.IsFailure) return Result.Failure<TransferDetailResponse>(planResult.Error);

            string createdByUsername = _currentTenant.Username ?? "unknown";

            Result<CardTransfer> transactionResult;
            try
            {
                transactionResult = await _unitOfWork.ExecuteInTransactionAsync<CardTransfer>(
                    () => _transferComposer.StageAsync(
                        tenantId, planResult.Value, branchRequestId: null, createdByUsername, cancellationToken),
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<TransferDetailResponse>(StockErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException)
            {
                return Result.Failure<TransferDetailResponse>(TransferErrors.PersistenceConflict());
            }

            if (transactionResult.IsFailure)
                return Result.Failure<TransferDetailResponse>(transactionResult.Error);

            CardTransfer committed = transactionResult.Value;

            // Audit second, after the id exists — see IAuditLogger.StageAction's doc comment for
            // why this is a deliberately separate save rather than inside the transaction above.
            _auditLogger.StageAction(
                tenantId, tenantId, _currentTenant.Username ?? "unknown",
                "Transfer", nameof(CardTransfer), committed.Id.ToString());
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            CardTransfer? detail = await _unitOfWork.CardTransfers.GetDetailAsync(committed.Id, tenantId, cancellationToken);
            return MapDetail(detail!);
        }

        // =====================================================================================
        //  Receive / Dispose (settlement)
        // =====================================================================================

        public async Task<Result<SettleTransferResult>> ReceiveAsync(
            long id, ReceiveTransferRequest request, CancellationToken cancellationToken = default)
        {
            Result<long> actor = ResolveWritingTenant();
            if (actor.IsFailure) return Result.Failure<SettleTransferResult>(actor.Error);
            long tenantId = actor.Value;

            if (request.Items is null || request.Items.Count == 0)
                return Result.Failure<SettleTransferResult>(TransferErrors.NoItems());

            var settlements = new Dictionary<long, LineSettlement>();
            foreach (ReceiveTransferLine line in request.Items)
            {
                if (!settlements.TryAdd(line.ProductId,
                    new LineSettlement(line.RealQuantityReceived, line.DisposedQuantity, line.ItemDispositions, line.DifferenceAction)))
                {
                    return Result.Failure<SettleTransferResult>(TransferErrors.DuplicateProduct(line.ProductId));
                }
            }

            return await SettleAsync(
                id, tenantId, settlements, request.DisposeReason, request.DisposingBranchId,
                request.ActionNotes, cancellationToken);
        }

        public async Task<Result<SettleTransferResult>> DisposeAsync(
            long id, DisposeTransferRequest request, CancellationToken cancellationToken = default)
        {
            Result<long> actor = ResolveWritingTenant();
            if (actor.IsFailure) return Result.Failure<SettleTransferResult>(actor.Error);
            long tenantId = actor.Value;

            string reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0) return Result.Failure<SettleTransferResult>(DisposalErrors.ReasonRequired());
            if (reason.Length > 500) return Result.Failure<SettleTransferResult>(DisposalErrors.ReasonTooLong(500));

            // Loaded once here so the "dispose everything" settlement plan can be built from the
            // transfer's own lines and items — the caller states no quantities at all.
            CardTransfer? transfer = await _unitOfWork.CardTransfers.GetForUpdateAsync(id, tenantId, cancellationToken);
            if (transfer is null) return Result.Failure<SettleTransferResult>(TransferErrors.NotFound(id));
            if (transfer.TransactionStatus != TransactionStatus.InProgress)
                return Result.Failure<SettleTransferResult>(TransferErrors.NotInProgress(id));

            // An Unknown-way line moves entitlement only - there is no physical card to write
            // off, so "dispose everything still open" cannot cover it (decision confirmed with
            // the repo owner). Rejected up front rather than silently skipped, matching this
            // endpoint's own "resolve everything open in one step" contract.
            CardTransferProduct? openUnknownLine = transfer.Products.FirstOrDefault(
                p => p.RealQuantityReceived is null && p.ProductTransactionWay == ProductTransactionWay.Unknown);
            if (openUnknownLine is not null)
                return Result.Failure<SettleTransferResult>(TransferErrors.DisposalNotAllowedForUnknown(openUnknownLine.ProductId));

            var settlements = new Dictionary<long, LineSettlement>();
            foreach (CardTransferProduct line in transfer.Products.Where(p => p.RealQuantityReceived is null))
            {
                IReadOnlyList<CardDispositionEntry> dispositions = transfer.Items
                    .Where(i => i.ProductItem.ProductId == line.ProductId)
                    .Select(i => new CardDispositionEntry(i.ProductItemId, TransactionItemReceiveStatus.Disposed))
                    .ToList();

                settlements[line.ProductId] = new LineSettlement(0, line.TransactedQuantity, dispositions, DifferenceAction: null);
            }

            return await SettleAsync(
                transfer, tenantId, settlements, reason, request.BranchId, null, cancellationToken);
        }

        /// <summary>
        /// Shared settlement path for <see cref="ReceiveAsync"/> and <see cref="DisposeAsync"/>.
        /// Loads the transfer, then delegates to the tracked-entity overload.
        /// </summary>
        private async Task<Result<SettleTransferResult>> SettleAsync(
            long id, long tenantId, IReadOnlyDictionary<long, LineSettlement> settlements,
            string? disposeReason, long? disposingBranchId, string? actionNotes, CancellationToken cancellationToken)
        {
            CardTransfer? transfer = await _unitOfWork.CardTransfers.GetForUpdateAsync(id, tenantId, cancellationToken);
            if (transfer is null) return Result.Failure<SettleTransferResult>(TransferErrors.NotFound(id));
            if (transfer.TransactionStatus != TransactionStatus.InProgress)
                return Result.Failure<SettleTransferResult>(TransferErrors.NotInProgress(id));

            return await SettleAsync(transfer, tenantId, settlements, disposeReason, disposingBranchId, actionNotes, cancellationToken);
        }

        /// <summary>
        /// The settlement itself. See the class-level doc comment for the stock rule this method
        /// applies uniformly, per line, regardless of whether <paramref name="transfer"/> is an
        /// ordinary transfer or an auto-generated return.
        /// </summary>
        private async Task<Result<SettleTransferResult>> SettleAsync(
            CardTransfer transfer, long tenantId, IReadOnlyDictionary<long, LineSettlement> settlements,
            string? disposeReason, long? disposingBranchId, string? actionNotes, CancellationToken cancellationToken)
        {
            // Both current callers already check this before reaching here; kept as a defensive
            // guard so a future third caller can't silently re-settle a closed transfer.
            if (transfer.TransactionStatus != TransactionStatus.InProgress)
                return Result.Failure<SettleTransferResult>(TransferErrors.NotInProgress(transfer.Id));

            // Maker-Checker workflow (Q1): the acting account's identity is always recorded, even
            // though it is fine — by design, given this system has one account per tenant — for
            // the Checker to be the same account as the Maker. Resolved once and reused both for
            // this transfer's own CheckedByUsername and for any auto-generated return's
            // CreatedByUsername (that return's "Maker" is whoever triggered this settlement).
            string checkedByUsername = _currentTenant.Username ?? "unknown";

            // ---- Validate the settlement covers exactly the transfer's still-open lines. -------
            // Both Known- and Unknown-way lines are open here (Unknown-way Maker-Checker
            // workflow) - RealQuantityReceived is null for either until this method runs. A line
            // only ever ends up excluded from openLines once it has actually been settled, by a
            // prior call to this same method (e.g. an auto-generated return already resolved).
            IReadOnlyList<CardTransferProduct> openLines = transfer.Products
                .Where(p => p.RealQuantityReceived is null)
                .ToList();

            foreach (long productId in settlements.Keys)
            {
                if (openLines.All(p => p.ProductId != productId))
                    return Result.Failure<SettleTransferResult>(TransferErrors.UnknownProductInSettlement(productId));
            }
            foreach (CardTransferProduct line in openLines)
            {
                if (!settlements.ContainsKey(line.ProductId))
                    return Result.Failure<SettleTransferResult>(TransferErrors.MissingProductInSettlement(line.ProductId));
            }

            // ---- Validate quantities and build a per-line plan. No writes yet. -----------------
            bool anyDisposed = settlements.Values.Any(s => s.Disposed > 0);
            if (anyDisposed)
            {
                if (string.IsNullOrWhiteSpace(disposeReason))
                    return Result.Failure<SettleTransferResult>(DisposalErrors.ReasonRequired());
                if (disposeReason.Trim().Length > 500)
                    return Result.Failure<SettleTransferResult>(DisposalErrors.ReasonTooLong(500));
                if (disposingBranchId is null)
                    return Result.Failure<SettleTransferResult>(TransferErrors.DisposingBranchRequired());
            }

            Branch? disposingBranch = null;
            if (anyDisposed)
            {
                (disposingBranch, Error? branchError) = await LoadDisposingBranchAsync(
                    disposingBranchId!.Value, tenantId, transfer, cancellationToken);
                if (branchError is not null) return Result.Failure<SettleTransferResult>(branchError);
            }

            bool anyReturned = false;
            var plans = new List<LinePlan>();

            foreach (CardTransferProduct line in openLines)
            {
                LineSettlement s = settlements[line.ProductId];
                int received = s.Received;
                int disposed = s.Disposed;

                if (received < 0 || disposed < 0 || received + disposed > line.TransactedQuantity)
                    return Result.Failure<SettleTransferResult>(TransferErrors.SettlementQuantityOutOfRange(line.ProductId));

                int returned = line.TransactedQuantity - received - disposed;

                List<CardTransferItem> lineItems = transfer.Items
                    .Where(i => i.ProductItem.ProductId == line.ProductId)
                    .ToList();

                if (line.ProductTransactionWay == ProductTransactionWay.Known)
                {
                    if (returned > 0) anyReturned = true;

                    if (s.DifferenceAction is not null)
                        return Result.Failure<SettleTransferResult>(TransferErrors.DifferenceActionNotApplicable(line.ProductId));

                    if (s.ItemDispositions is null || s.ItemDispositions.Count == 0)
                        return Result.Failure<SettleTransferResult>(TransferErrors.DispositionsRequired(line.ProductId));
                    if (s.ItemDispositions.Count != lineItems.Count)
                        return Result.Failure<SettleTransferResult>(TransferErrors.DispositionCountMismatch(line.ProductId));

                    var lineItemIds = lineItems.Select(i => i.ProductItemId).ToHashSet();
                    var seen = new HashSet<long>();
                    int dr = 0, dd = 0, dx = 0;

                    foreach (CardDispositionEntry entry in s.ItemDispositions)
                    {
                        if (!lineItemIds.Contains(entry.ProductItemId))
                            return Result.Failure<SettleTransferResult>(TransferErrors.DispositionItemNotInTransfer(entry.ProductItemId));
                        if (!seen.Add(entry.ProductItemId))
                            return Result.Failure<SettleTransferResult>(TransferErrors.DuplicateItem(entry.ProductItemId));

                        switch (entry.Disposition)
                        {
                            case TransactionItemReceiveStatus.Received: dr++; break;
                            case TransactionItemReceiveStatus.Disposed: dd++; break;
                            case TransactionItemReceiveStatus.NotReceived: dx++; break;
                            default:
                                return Result.Failure<SettleTransferResult>(
                                    TransferErrors.PendingDispositionNotAllowed(entry.ProductItemId));
                        }
                    }

                    if (dr != received || dd != disposed || dx != returned)
                        return Result.Failure<SettleTransferResult>(TransferErrors.DispositionCountMismatch(line.ProductId));

                    plans.Add(new LinePlan(line, received, disposed, returned, lineItems, s.ItemDispositions, DifferenceAction: null));
                }
                else
                {
                    // Unknown-way, open (Maker-Checker workflow) — no cards, so no per-item
                    // dispositions and no disposal (decision confirmed with the repo owner): the
                    // line is settled purely as quantities, and any remainder must be resolved
                    // explicitly rather than inferred.
                    if (s.ItemDispositions is { Count: > 0 })
                        return Result.Failure<SettleTransferResult>(TransferErrors.ItemIdsNotAllowedForUnknown(line.ProductId));
                    if (disposed > 0)
                        return Result.Failure<SettleTransferResult>(TransferErrors.DisposalNotAllowedForUnknown(line.ProductId));

                    if (returned > 0)
                    {
                        if (s.DifferenceAction is null)
                            return Result.Failure<SettleTransferResult>(TransferErrors.DifferenceActionRequired(line.ProductId));
                        if (s.DifferenceAction is not (TransferDifferenceAction.ReturnedToSource or TransferDifferenceAction.KeptAtDestination))
                            return Result.Failure<SettleTransferResult>(TransferErrors.InvalidDifferenceAction(line.ProductId));

                        // Only a "returned" resolution spawns an auto-generated return transfer
                        // (mirroring Known-way, decision confirmed with the repo owner) — a "kept"
                        // resolution settles entirely at the target, nothing goes back anywhere.
                        if (s.DifferenceAction == TransferDifferenceAction.ReturnedToSource) anyReturned = true;
                    }
                    else if (s.DifferenceAction is not null)
                    {
                        return Result.Failure<SettleTransferResult>(TransferErrors.DifferenceActionNotApplicable(line.ProductId));
                    }

                    plans.Add(new LinePlan(
                        line, received, Disposed: 0, returned,
                        Items: Array.Empty<CardTransferItem>(), Dispositions: Array.Empty<CardDispositionEntry>(),
                        DifferenceAction: s.DifferenceAction));
                }
            }

            // A remainder needs somewhere to go back to. Refused up front, before any write,
            // rather than receiving part of the shipment and stranding the rest.
            Branch? returnTargetBranch = null;
            if (anyReturned)
            {
                returnTargetBranch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(
                    transfer.SourceBranchId, cancellationToken);
                if (returnTargetBranch is null || returnTargetBranch.IsDeleted || !returnTargetBranch.IsActive)
                    return Result.Failure<SettleTransferResult>(TransferErrors.ReturnBranchUnavailable(transfer.SourceBranchId));
            }

            // ---- Apply. Everything above was read-only; everything below writes. --------------
            // disposal, returnTransfer and the running totals are declared here — outside the
            // transaction lambda — and only mutated inside it (closure capture, the same pattern
            // BatchUploadService uses for its own counters). This is not a style preference: a
            // newly inserted entity's Id is 0 until SaveChanges actually runs, which happens
            // *after* the lambda returns, so reading returnTransfer.Id or disposal.Id from inside
            // the lambda — or baking either into a value returned from it — would capture 0. The
            // non-generic ExecuteInTransactionAsync is used for exactly this reason: nothing
            // meaningful needs to come out of the lambda's return value here.
            CardDisposal? disposal = null;
            CardTransfer? returnTransfer = null;
            int totalReceived = 0, totalDisposed = 0, totalReturned = 0;

            Result transactionResult;
            try
            {
                transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    foreach (LinePlan plan in plans)
                    {
                        CardTransferProduct line = plan.Line;
                        line.RealQuantityReceived = plan.Received;
                        line.DisposedQuantity = plan.Disposed;

                        totalReceived += plan.Received;
                        totalDisposed += plan.Disposed;
                        totalReturned += plan.Returned;

                        var byId = plan.Dispositions.ToDictionary(d => d.ProductItemId, d => d.Disposition);
                        var returningItems = new List<ProductItem>();

                        foreach (CardTransferItem item in plan.Items)
                        {
                            TransactionItemReceiveStatus outcome = byId[item.ProductItemId];
                            item.ReceiveStatus = outcome;
                            ProductItem card = item.ProductItem;

                            switch (outcome)
                            {
                                case TransactionItemReceiveStatus.Received:
                                    card.BranchID = transfer.TargetBranchId;
                                    card.Status = CardStatus.Available;
                                    break;

                                case TransactionItemReceiveStatus.Disposed:
                                    card.Status = CardStatus.Disposed;
                                    card.BranchID = disposingBranch!.Id;   // final resting location
                                    disposal ??= new CardDisposal
                                    {
                                        TenantId = tenantId,
                                        BranchId = disposingBranch.Id,
                                        CardTransferId = transfer.Id,
                                        DisposedByTenantId = tenantId,
                                        DisposedAt = DateTime.UtcNow,
                                        Reason = disposeReason!.Trim(),
                                    };
                                    disposal.Items.Add(new CardDisposalItem { TenantId = tenantId, ProductItemId = card.ID });
                                    break;

                                case TransactionItemReceiveStatus.NotReceived:
                                    // Stays BranchID = null, Status = OnHold — already was, since
                                    // create time. This row is the historical record of the
                                    // outbound leg; the return transfer gets a fresh row below.
                                    returningItems.Add(card);
                                    break;
                            }
                        }

                        // ---- Stock: the rule from the class doc, applied to this line. --------
                        Stock sourceStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                            tenantId, transfer.SourceBranchId, line.ProductId, cancellationToken);

                        int releasedHold = sourceStock.HoldQuantity - line.TransactedQuantity;
                        if (releasedHold < 0)
                        {
                            // Should be unreachable: Hold was incremented by exactly this amount
                            // at create time and nothing else touches it before this release. A
                            // negative result here means the stock aggregate and this transfer's
                            // own bookkeeping have diverged — a StockInconsistency, not an
                            // ordinary availability shortfall (StockErrors.InsufficientAvailable
                            // is about Available going negative, a different column and a
                            // different, caller-triggerable condition).
                            return Result.Failure(
                                TransferErrors.StockInconsistency(transfer.SourceBranchId, line.ProductId));
                        }
                        sourceStock.HoldQuantity = releasedHold;
                        sourceStock.UpdatedAt = DateTime.UtcNow;

                        // ---- Target credit: Known and Unknown/ReturnedToSource both leave the
                        // remainder in transit (Hold, pending its own return-leg receive);
                        // Unknown/KeptAtDestination instead settles the whole quantity at the
                        // target immediately, with nothing left over to send anywhere.
                        int availableCredit = plan.Received;
                        int holdCredit = plan.Returned;
                        if (line.ProductTransactionWay == ProductTransactionWay.Unknown &&
                            plan.DifferenceAction == TransferDifferenceAction.KeptAtDestination)
                        {
                            availableCredit += plan.Returned;
                            holdCredit = 0;
                        }

                        if (availableCredit > 0 || holdCredit > 0)
                        {
                            Stock targetStock = await _unitOfWork.Stocks.GetOrCreateForUpdateAsync(
                                tenantId, transfer.TargetBranchId, line.ProductId, cancellationToken);
                            targetStock.AvailableQuantity += availableCredit;
                            targetStock.HoldQuantity += holdCredit;
                            targetStock.UpdatedAt = DateTime.UtcNow;
                        }

                        // ---- Return leg: fresh line + fresh item rows on the return transfer. -
                        // Gated on holdCredit, not the raw remainder — a KeptAtDestination
                        // resolution has a remainder but sends nothing back anywhere.
                        if (holdCredit > 0)
                        {
                            returnTransfer ??= new CardTransfer
                            {
                                TenantId = tenantId,
                                BranchRequestId = transfer.BranchRequestId,   // D-04: return inherits its parent's request
                                CreatedAt = DateTime.UtcNow,
                                CreatedByTenantId = tenantId,
                                CreatedByUsername = checkedByUsername,   // the Checker who settled the parent is this return's Maker
                                SourceBranchId = transfer.TargetBranchId,
                                TargetBranchId = transfer.SourceBranchId,
                                TransactionStatus = TransactionStatus.InProgress,
                                Origin = TransactionOrigin.AutoGeneratedReturn,
                                ParentTransferId = transfer.Id,
                            };

                            returnTransfer.Products.Add(new CardTransferProduct
                            {
                                TenantId = tenantId,
                                ProductId = line.ProductId,
                                TransactedQuantity = plan.Returned,
                                ProductTransactionWay = line.ProductTransactionWay,   // carried forward
                            });

                            foreach (ProductItem card in returningItems)
                            {
                                returnTransfer.Items.Add(new CardTransferItem
                                {
                                    TenantId = tenantId,
                                    ProductItemId = card.ID,
                                    ReceiveStatus = TransactionItemReceiveStatus.Pending,
                                });
                            }
                        }
                    }

                    // API §4.9, decision D-04: credit the fulfilling request's counters when this
                    // transfer settles a request line. Plans built here can now be Known- or
                    // Unknown-way (Unknown-way Maker-Checker workflow — it no longer settles at
                    // create time, §1.4/§1.7) — no branching on ProductTransactionWay is needed at
                    // this call site regardless, since it only ever reads `Received` per line.
                    if (transfer.BranchRequestId is long brId)
                    {
                        var receivedByProductId = plans.ToDictionary(p => p.Line.ProductId, p => p.Received);
                        await _branchRequestFulfilment.ApplyReceiptAsync(
                            brId, transfer.TargetBranchId, receivedByProductId, cancellationToken);
                    }

                    // ---- This transfer's own final status. ------------------------------------
                    transfer.TransactionStatus =
                        totalReturned == 0 && totalDisposed == 0 ? TransactionStatus.Received :
                        totalReceived == 0 && totalReturned == 0 ? TransactionStatus.Disposed :
                        totalReceived == 0 && totalDisposed == 0 ? TransactionStatus.ReturnedBack :
                        TransactionStatus.PartiallyReceived;
                    transfer.StatusChangedAt = DateTime.UtcNow;
                    transfer.CheckedByUsername = checkedByUsername;
                    if (!string.IsNullOrWhiteSpace(actionNotes)) transfer.ActionNotes = actionNotes;

                    if (disposal is not null) await _unitOfWork.CardDisposals.AddAsync(disposal, cancellationToken);
                    if (returnTransfer is not null) await _unitOfWork.CardTransfers.AddAsync(returnTransfer, cancellationToken);

                    return Result.Success();
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<SettleTransferResult>(TransferErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException)
            {
                return Result.Failure<SettleTransferResult>(TransferErrors.PersistenceConflict());
            }

            if (transactionResult.IsFailure)
                return Result.Failure<SettleTransferResult>(transactionResult.Error);

            // disposal.Id and returnTransfer.Id are trustworthy now — SaveChanges has run.
            var result = new SettleTransferResult(
                transfer.Id, transfer.TransactionStatus,
                returnTransfer?.Id, disposal?.Id,
                totalReceived, totalDisposed, totalReturned);

            string action = result.TransactionStatus switch
            {
                TransactionStatus.Received => "Received",
                TransactionStatus.Disposed => "Disposed",
                TransactionStatus.ReturnedBack => "Returned",
                _ => "PartiallyReceived",
            };
            _auditLogger.StageAction(
                tenantId, tenantId, _currentTenant.Username ?? "unknown",
                action, nameof(CardTransfer), transfer.Id.ToString(),
                anyDisposed ? disposeReason!.Trim() : null);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return result;
        }

        // =====================================================================================
        //  Validation helpers
        // =====================================================================================

        private static Result ValidateCreateShape(CreateTransferRequest request)
        {
            if (request.Items is null || request.Items.Count == 0)
                return Result.Failure(TransferErrors.NoItems());

            if (request.ActionNotes is { Length: > 500 })
                return Result.Failure(TransferErrors.ActionNotesTooLong(500));

            var seenProducts = new HashSet<long>();
            foreach (CreateTransferLine line in request.Items)
            {
                if (!seenProducts.Add(line.ProductId))
                    return Result.Failure(TransferErrors.DuplicateProduct(line.ProductId));
                if (line.TransactedQuantity <= 0)
                    return Result.Failure(TransferErrors.InvalidQuantity(line.ProductId));
            }

            // The per-transfer card cap (EC-25) is enforced against the sum of quantities, not
            // the line count — a transfer with one line moving 500 cards is the same load on the
            // system as one with 500 single-card lines.
            const int maxCardsPerTransfer = 500;
            int totalQuantity = request.Items.Sum(i => i.TransactedQuantity);
            if (totalQuantity > maxCardsPerTransfer)
                return Result.Failure(TransferErrors.TooManyItems(maxCardsPerTransfer));

            return Result.Success();
        }

        private async Task<(Branch? Branch, Error? Error)> LoadDisposingBranchAsync(
            long branchId, long tenantId, CardTransfer transfer, CancellationToken cancellationToken)
        {
            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(branchId, cancellationToken);
            if (branch is null || branch.TenantId != tenantId)
                return (null, DisposalErrors.BranchNotFound(branchId));
            if (branch.IsDeleted)
                return (null, DisposalErrors.BranchDeleted(branchId));
            // Closest enforceable form of "only a party to the transfer may dispose of its cards"
            // (decision Q6) — the JWT carries no branch claim to check against directly.
            if (branch.Id != transfer.SourceBranchId && branch.Id != transfer.TargetBranchId)
                return (null, DisposalErrors.BranchNotPartyToTransfer(branchId));
            return (branch, null);
        }

        // null scope => system admin (read-only, decision Q7); otherwise the tenant caller's id.
        private long? ResolveReadScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = TransferErrors.ActorNotResolved();
            return null;
        }

        // Every write in this service rejects a system admin outright (decision Q7): a transfer's
        // CreatedByTenantId has no admin id to point to, and admin access here is read-only by design.
        private Result<long> ResolveWritingTenant()
        {
            if (_currentTenant.IsSystemAdmin) return Result.Failure<long>(TransferErrors.SystemAdminNotAllowed());
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            return Result.Failure<long>(TransferErrors.ActorNotResolved());
        }

        // =====================================================================================
        //  Mapping
        // =====================================================================================

        private static TransferListItemResponse MapListItem(CardTransfer t) => new(
            t.Id, t.TenantId,
            t.SourceBranchId, t.SourceBranch.Name,
            t.TargetBranchId, t.TargetBranch.Name,
            t.TransactionStatus, t.Origin, t.ParentTransferId, t.BranchRequestId,
            t.Products.Count, t.Products.Sum(p => p.TransactedQuantity),
            t.CreatedAt, t.StatusChangedAt);

        /// <summary>
        /// Maps a fully-loaded transfer to its detail response. Internal (not private): reused by
        /// <see cref="BranchRequestService.ConfirmAsync"/> (API §4.9, decision Q-12) so a
        /// confirm's generated transfers are mapped through the exact same code a direct
        /// <c>GET /api/inventory/transactions/{id}</c> call uses, rather than a second copy of
        /// this logic living in a sibling service.
        /// </summary>
        internal static TransferDetailResponse MapDetail(CardTransfer t)
        {
            var knownProductIds = t.Products
                .Where(p => p.ProductTransactionWay == ProductTransactionWay.Known)
                .Select(p => p.ProductId)
                .ToHashSet();

            IReadOnlyList<TransferItemResponse> items = t.Items
                .Where(i => knownProductIds.Contains(i.ProductItem.ProductId))
                .Select(i => new TransferItemResponse(
                    i.ProductItemId, i.ProductItem.MaskedPan, i.ProductItem.ProductId, i.ReceiveStatus))
                .ToList();

            return new TransferDetailResponse(
                t.Id, t.TenantId,
                t.SourceBranchId, t.SourceBranch.Name,
                t.TargetBranchId, t.TargetBranch.Name,
                t.TransactionStatus, t.Origin, t.ParentTransferId, t.BranchRequestId,
                t.ActionNotes, t.CreatedAt, t.CreatedByTenantId, t.CreatedByUsername,
                t.StatusChangedAt, t.CheckedByUsername,
                Convert.ToBase64String(t.RowVersion),
                t.Products.Select(MapProductLine).ToList(),
                items);
        }

        private static TransferProductResponse MapProductLine(CardTransferProduct p)
        {
            if (p.RealQuantityReceived is null)
            {
                return new TransferProductResponse(
                    p.ProductId, p.Product?.Name ?? string.Empty, p.TransactedQuantity,
                    null, null, 0, p.ProductTransactionWay, ProductReceiveOutcome.Pending, p.DifferenceAction);
            }

            int received = p.RealQuantityReceived.Value;
            int disposed = p.DisposedQuantity ?? 0;
            int returned = p.TransactedQuantity - received - disposed;

            ProductReceiveOutcome outcome =
                disposed == p.TransactedQuantity ? ProductReceiveOutcome.FullyDisposed :
                received == p.TransactedQuantity ? ProductReceiveOutcome.FullyReceived :
                received == 0 ? ProductReceiveOutcome.NotReceived :
                ProductReceiveOutcome.PartialReceived;

            return new TransferProductResponse(
                p.ProductId, p.Product?.Name ?? string.Empty, p.TransactedQuantity,
                received, disposed, returned, p.ProductTransactionWay, outcome, p.DifferenceAction);
        }

        /// <summary>Caller-stated settlement for one product line, before validation.</summary>
        private readonly record struct LineSettlement(
            int Received, int Disposed, IReadOnlyList<CardDispositionEntry>? ItemDispositions,
            TransferDifferenceAction? DifferenceAction);

        /// <summary>Validated, ready-to-apply settlement plan for one product line.</summary>
        private sealed record LinePlan(
            CardTransferProduct Line,
            int Received,
            int Disposed,
            int Returned,
            IReadOnlyList<CardTransferItem> Items,
            IReadOnlyList<CardDispositionEntry> Dispositions,
            TransferDifferenceAction? DifferenceAction);
    }
}
