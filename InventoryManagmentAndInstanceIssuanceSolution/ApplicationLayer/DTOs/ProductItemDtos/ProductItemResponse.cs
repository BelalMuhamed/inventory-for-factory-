using System;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.ProductItems
{
    /// <summary>
    /// Product-item projection (API Spec §4.7). The PAN is never returned in full:
    /// <see cref="MaskedPan"/> is the persisted display value — ten masking characters followed
    /// by the last six PAN digits (e.g. <c>**********123456</c>). The card's identity/dedup
    /// fingerprint is never included on this or any other DTO.
    /// <para>
    /// <see cref="BranchId"/> is nullable (Transactions §4.10, Q4): <c>null</c> means the card is
    /// in transit under a transfer, or sits in the tenant's unassigned pool awaiting a branch at
    /// print time. Clients must render that case rather than assuming a branch is always present.
    /// </para>
    /// </summary>
    public sealed record ProductItemResponse(
        long Id,
        long TenantId,
        string MaskedPan,
        long ProductId,
        string ProductName,
        long? BranchId,
        long BatchId,
        CardStatus Status,
        string? HolderName,
        string? Notes,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    /// <summary>
    /// Update payload for <c>PUT /api/product-items/{id}</c> (API Spec §4.7). Changing
    /// <see cref="Status"/> transactionally recomputes the branch stock aggregate.
    /// </summary>
    public sealed record UpdateProductItemRequest(
        CardStatus Status,
        string? HolderName,
        string? Notes);

    /// <summary>
    /// Filter/paging inputs for <c>GET /api/product-items</c> (API Spec §4.7).
    /// </summary>
    /// <param name="Code">Substring match against the stored <c>MaskedPan</c> (i.e. the last six PAN digits).</param>
    /// <param name="ProductId">Optional exact product filter.</param>
    /// <param name="ProductName">Optional case-insensitive product-name contains-filter.</param>
    /// <param name="Status">Optional status filter.</param>
    /// <param name="BranchId">Optional branch filter.</param>
    /// <param name="IsDeleted">Tri-state (null = both).</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="SortBy">code | status | productid | branchid | createdat (default).</param>
    /// <param name="SortDir">asc (default) or desc.</param>
    public sealed record ProductItemListFilter(
        string? Code = null,
        long? ProductId = null,
        string? ProductName = null,
        CardStatus? Status = null,
        long? BranchId = null,
        bool? IsDeleted = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");
}