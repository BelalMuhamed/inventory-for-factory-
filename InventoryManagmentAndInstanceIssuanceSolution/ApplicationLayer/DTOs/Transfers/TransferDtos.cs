using System;
using System.Collections.Generic;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Transfers
{
    // =====================================================================================
    //  Requests
    // =====================================================================================

    /// <summary>
    /// One product line on a new transfer.
    /// </summary>
    /// <param name="ProductId">Product being moved.</param>
    /// <param name="TransactedQuantity">How many cards to send. Must be greater than zero.</param>
    /// <param name="ProductItemIds">
    /// The specific cards being sent. <b>Required</b> for a Known-way product, and the count must
    /// equal <paramref name="TransactedQuantity"/> (decision Q3). <b>Rejected</b> for an
    /// Unknown-way product, where the system selects cards itself.
    /// </param>
    public sealed record CreateTransferLine(
        long ProductId,
        int TransactedQuantity,
        IReadOnlyList<long>? ProductItemIds = null);

    /// <summary>
    /// Payload for <c>POST /api/inventory/transactions</c> (API §4.10). Creates a direct transfer;
    /// request-driven transfers arrive via §4.9 instead.
    /// </summary>
    /// <param name="SourceBranchId">Branch the cards leave. May be inactive; may not be deleted.</param>
    /// <param name="TargetBranchId">Branch the cards are headed to. Must differ from the source.</param>
    /// <param name="Items">Product lines. At least one, with no repeated product.</param>
    /// <param name="ActionNotes">Optional free-text note stored on the transfer.</param>
    public sealed record CreateTransferRequest(
        long SourceBranchId,
        long TargetBranchId,
        IReadOnlyList<CreateTransferLine> Items,
        string? ActionNotes = null);

    /// <summary>
    /// The outcome of one card in a Known-way settlement.
    /// </summary>
    /// <param name="ProductItemId">The card being settled. Must belong to the transfer.</param>
    /// <param name="Disposition">
    /// <see cref="TransactionItemReceiveStatus.Received"/>,
    /// <see cref="TransactionItemReceiveStatus.NotReceived"/> (goes back under the auto-generated
    /// return), or <see cref="TransactionItemReceiveStatus.Disposed"/>.
    /// <see cref="TransactionItemReceiveStatus.Pending"/> is rejected — settlement must resolve
    /// every card.
    /// </param>
    public sealed record CardDispositionEntry(
        long ProductItemId,
        TransactionItemReceiveStatus Disposition);

    /// <summary>
    /// How the target settled one product line.
    /// </summary>
    /// <param name="ProductId">Product being settled. Must be carried by the transfer.</param>
    /// <param name="RealQuantityReceived">Quantity accepted into the target branch.</param>
    /// <param name="DisposedQuantity">Quantity written off instead of accepted or returned.</param>
    /// <param name="ItemDispositions">
    /// Per-card outcomes. Required for a Known-way line and must account for every card on it;
    /// rejected for an Unknown-way line.
    /// </param>
    /// <param name="DifferenceAction">
    /// How to resolve an Unknown-way line's remainder (<c>RealQuantityReceived &lt;
    /// TransactedQuantity</c>). Required exactly when that remainder is greater than zero on an
    /// Unknown-way line; rejected otherwise — including for any Known-way line, whose remainder is
    /// always resolved per card via <paramref name="ItemDispositions"/> instead. Disposal
    /// (<paramref name="DisposedQuantity"/> &gt; 0) is not supported for an Unknown-way line.
    /// </param>
    public sealed record ReceiveTransferLine(
        long ProductId,
        int RealQuantityReceived,
        int DisposedQuantity,
        IReadOnlyList<CardDispositionEntry>? ItemDispositions = null,
        TransferDifferenceAction? DifferenceAction = null);

    /// <summary>
    /// Payload for <c>POST /api/inventory/transactions/{id}/receive</c> (API §4.10, Addendum A
    /// §2.3). This single call expresses every settlement outcome — there is no separate refuse
    /// endpoint.
    /// <para>
    /// Per line, <c>received + disposed</c> may not exceed what was sent; whatever is left over
    /// becomes an auto-generated return transfer back to the source, which that branch must then
    /// settle in its own right.
    /// </para>
    /// <para>
    /// Every product carried by the transfer must appear in <paramref name="Items"/>. An omitted
    /// line is an error, not an implicit zero: partial receipt moves real stock, so it is stated
    /// rather than inferred.
    /// </para>
    /// </summary>
    /// <param name="Items">One entry per product carried by the transfer.</param>
    /// <param name="DisposeReason">
    /// Why cards were written off. Required — and only permitted — when any
    /// <see cref="ReceiveTransferLine.DisposedQuantity"/> is greater than zero.
    /// </param>
    /// <param name="DisposingBranchId">
    /// Branch accountable for the write-off. Required when anything is disposed. Cards being
    /// settled sit at no branch (<c>BranchID IS NULL</c>), so this cannot be derived.
    /// </param>
    /// <param name="ActionNotes">Optional free-text note stored on the transfer.</param>
    public sealed record ReceiveTransferRequest(
        IReadOnlyList<ReceiveTransferLine> Items,
        string? DisposeReason = null,
        long? DisposingBranchId = null,
        string? ActionNotes = null);

    /// <summary>
    /// Payload for <c>POST /api/inventory/transactions/{id}/dispose</c> (Addendum A §2.4): write
    /// off everything the transfer still carries, in one step, without receiving any of it.
    /// </summary>
    /// <param name="BranchId">Branch accountable for the write-off. Must be a party to the transfer.</param>
    /// <param name="Reason">Why the cards were written off. Required, non-empty.</param>
    public sealed record DisposeTransferRequest(
        long BranchId,
        string Reason);

    // =====================================================================================
    //  Responses
    // =====================================================================================

    /// <summary>
    /// Query-time receipt outcome for one product line (ERD §4.5: "this is a DTO concern — not a
    /// column"). Derived from the transacted, received and disposed quantities, which is why it
    /// lives in the DTO namespace rather than <c>DomainLayer.Enums</c>.
    /// </summary>
    public enum ProductReceiveOutcome
    {
        /// <summary>The transfer has not been settled yet.</summary>
        Pending = 0,

        /// <summary>Every card sent was received (ERD §4.5: transacted equals received).</summary>
        FullyReceived = 1,

        /// <summary>Some but not all cards were received (ERD §4.5: 0 &lt; received &lt; transacted).</summary>
        PartialReceived = 2,

        /// <summary>Nothing was received; the line was returned, written off, or both.</summary>
        NotReceived = 3,

        /// <summary>Every card sent was written off.</summary>
        FullyDisposed = 4
    }

    /// <summary>One product line as returned by the transfer detail endpoint.</summary>
    /// <param name="ReturnedQuantity">
    /// Derived as <c>transacted − received − disposed</c>. Not stored: a persisted copy could
    /// drift from the three values it is computed from.
    /// </param>
    /// <param name="DifferenceAction">
    /// How this line's remainder was, or will be, resolved — set only for an Unknown-way line
    /// once it has a remainder (<c>null</c> for a Known-way line, and for any line with nothing
    /// left over). Surfaced explicitly rather than left implicit in the quantities, per the
    /// Unknown-way Maker-Checker workflow's audit requirement that the difference never be
    /// silently hidden.
    /// </param>
    public sealed record TransferProductResponse(
        long ProductId,
        string ProductName,
        int TransactedQuantity,
        int? RealQuantityReceived,
        int? DisposedQuantity,
        int ReturnedQuantity,
        ProductTransactionWay ProductTransactionWay,
        ProductReceiveOutcome Outcome,
        TransferDifferenceAction? DifferenceAction);

    /// <summary>
    /// One card on a Known-way transfer. Identified by its masked PAN — the full PAN is never
    /// stored and the fingerprint is never disclosed.
    /// </summary>
    public sealed record TransferItemResponse(
        long ProductItemId,
        string MaskedPan,
        long ProductId,
        TransactionItemReceiveStatus ReceiveStatus);

    /// <summary>Row shape for <c>GET /api/inventory/transactions</c> and the history alias.</summary>
    public sealed record TransferListItemResponse(
        long Id,
        long TenantId,
        long SourceBranchId,
        string SourceBranchName,
        long TargetBranchId,
        string TargetBranchName,
        TransactionStatus TransactionStatus,
        TransactionOrigin Origin,
        long? ParentTransferId,
        long? BranchRequestId,
        int ProductLineCount,
        int TotalTransactedQuantity,
        DateTime CreatedAt,
        DateTime? StatusChangedAt);

    /// <summary>
    /// Full transfer as returned by <c>GET /api/inventory/transactions/{id}</c>.
    /// <see cref="Items"/> is empty for a transfer carrying only Unknown-way lines.
    /// </summary>
    public sealed record TransferDetailResponse(
        long Id,
        long TenantId,
        long SourceBranchId,
        string SourceBranchName,
        long TargetBranchId,
        string TargetBranchName,
        TransactionStatus TransactionStatus,
        TransactionOrigin Origin,
        long? ParentTransferId,
        long? BranchRequestId,
        string? ActionNotes,
        DateTime CreatedAt,
        long CreatedByTenantId,
        string CreatedByUsername,
        DateTime? StatusChangedAt,
        string? CheckedByUsername,
        string RowVersion,
        IReadOnlyList<TransferProductResponse> Products,
        IReadOnlyList<TransferItemResponse> Items);

    /// <summary>
    /// Outcome of settling a transfer. <see cref="ReturnTransferId"/> is the auto-generated return
    /// carrying whatever was neither received nor written off, or <c>null</c> when nothing was
    /// left over.
    /// </summary>
    public sealed record SettleTransferResult(
        long TransferId,
        TransactionStatus TransactionStatus,
        long? ReturnTransferId,
        long? DisposalId,
        int TotalReceived,
        int TotalDisposed,
        int TotalReturned);

    // =====================================================================================
    //  Filters
    // =====================================================================================

    /// <summary>
    /// Filter and paging inputs for the transfer list endpoints (API §3.1, §4.10). Bound from the
    /// query string.
    /// </summary>
    /// <param name="Status">Optional lifecycle filter.</param>
    /// <param name="SourceBranchId">Optional source-branch filter.</param>
    /// <param name="TargetBranchId">Optional target-branch filter.</param>
    /// <param name="BranchId">
    /// Matches transfers where this branch is <em>either</em> source or target — the "everything
    /// that touched my branch" view (API §4.10 scope note).
    /// </param>
    /// <param name="ProductId">Optional filter on a product carried by the transfer.</param>
    /// <param name="Origin">Separates user-raised transfers from auto-generated returns.</param>
    /// <param name="ParentTransferId">Finds the return produced by a given transfer.</param>
    /// <param name="BranchRequestId">Reserved for §4.9; matches nothing today.</param>
    /// <param name="FromDate">Inclusive lower bound on <c>CreatedAt</c> (UTC).</param>
    /// <param name="ToDate">Inclusive upper bound on <c>CreatedAt</c> (UTC).</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100). Defaults to 20.</param>
    /// <param name="SortBy">createdat (default) | statuschangedat | status | sourcebranchid | targetbranchid.</param>
    /// <param name="SortDir">
    /// asc or desc. Defaults to <c>desc</c> — unlike the other list endpoints, because the useful
    /// view of movement history is newest first, and the ERD §4.3 index is built that way.
    /// </param>
    public sealed record TransferListFilter(
        TransactionStatus? Status = null,
        long? SourceBranchId = null,
        long? TargetBranchId = null,
        long? BranchId = null,
        long? ProductId = null,
        TransactionOrigin? Origin = null,
        long? ParentTransferId = null,
        long? BranchRequestId = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "desc");
}
