using System;
using System.Collections.Generic;

namespace ApplicationLayer.DTOs.Disposals
{
    /// <summary>
    /// Quantity of one product to write off, when the caller does not name specific cards.
    /// </summary>
    /// <param name="ProductId">Product being written off.</param>
    /// <param name="Quantity">How many cards. Must be greater than zero.</param>
    public sealed record DisposeCardsLine(
        long ProductId,
        int Quantity);

    /// <summary>
    /// Payload for <c>POST /api/inventory/cards/dispose</c> (Addendum A §2.4): write off cards
    /// sitting at a branch, outside any transfer — damaged, spoiled, or discontinued stock.
    /// <para>
    /// Exactly one of <paramref name="ProductItemIds"/> and <paramref name="Items"/> must be
    /// supplied. Supplying both is refused rather than resolved by precedence: guessing which the
    /// caller meant risks destroying the wrong cards, and disposal cannot be undone.
    /// </para>
    /// </summary>
    /// <param name="BranchId">
    /// Branch performing the disposal and whose available stock is decremented. Required — the
    /// cards' own branch is not consulted, so that this always names who is accountable.
    /// </param>
    /// <param name="Reason">Why the cards were written off. Required, non-empty after trimming.</param>
    /// <param name="ProductItemIds">
    /// The exact cards to write off. Every one must currently be available at
    /// <paramref name="BranchId"/>.
    /// </param>
    /// <param name="Items">
    /// Quantities per product, letting the system choose which cards to consume (oldest first).
    /// The chosen cards are still recorded individually on the disposal, so the record of what
    /// left inventory is exact either way.
    /// </param>
    public sealed record DisposeCardsRequest(
        long BranchId,
        string Reason,
        IReadOnlyList<long>? ProductItemIds = null,
        IReadOnlyList<DisposeCardsLine>? Items = null);

    /// <summary>One card recorded on a disposal.</summary>
    public sealed record DisposalItemResponse(
        long ProductItemId,
        string MaskedPan,
        long ProductId,
        string ProductName);

    /// <summary>Row shape for the disposal list endpoint.</summary>
    public sealed record DisposalListItemResponse(
        long Id,
        long TenantId,
        long BranchId,
        string BranchName,
        long? CardTransferId,
        string Reason,
        int CardCount,
        DateTime DisposedAt);

    /// <summary>Full disposal, including every card written off under it.</summary>
    public sealed record DisposalDetailResponse(
        long Id,
        long TenantId,
        long BranchId,
        string BranchName,
        long? CardTransferId,
        long DisposedByTenantId,
        string Reason,
        DateTime DisposedAt,
        IReadOnlyList<DisposalItemResponse> Items);

    /// <summary>
    /// Filter and paging inputs for the disposal list endpoint (API §3.1).
    /// </summary>
    /// <param name="BranchId">Optional disposing-branch filter.</param>
    /// <param name="ProductId">Optional filter on a product written off.</param>
    /// <param name="CardTransferId">Finds the disposal that settled a given transfer.</param>
    /// <param name="TransferRelatedOnly">
    /// Tri-state: <c>true</c> keeps only disposals made while settling a transfer, <c>false</c>
    /// only standalone ones, <c>null</c> both.
    /// </param>
    /// <param name="FromDate">Inclusive lower bound on <c>DisposedAt</c> (UTC).</param>
    /// <param name="ToDate">Inclusive upper bound on <c>DisposedAt</c> (UTC).</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100). Defaults to 20.</param>
    /// <param name="SortBy">disposedat (default) | branchid.</param>
    /// <param name="SortDir">asc or desc. Defaults to <c>desc</c>: newest write-offs first.</param>
    public sealed record DisposalListFilter(
        long? BranchId = null,
        long? ProductId = null,
        long? CardTransferId = null,
        bool? TransferRelatedOnly = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "desc");
}
