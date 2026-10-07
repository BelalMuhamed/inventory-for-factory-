using System;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Products
{
    /// <summary>Product projection returned to clients (no entity leaks past Application).</summary>
    public sealed record ProductResponse(
        long Id,
        long TenantId,
        string Name,
        ActivationStatus ActivationStatus,
        int LowProductThreshold,
        ProductTransactionWay ProductTransactionWay,
        UsingPrinterType UsingPrinterType,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        DateTime? DeletedAt);
        // NOTE (stock seam): when Stock (ERD §3.1) lands, the GET /api/products/{id} detail will
        // also surface aggregated stock across branches. That is a richer detail DTO, kept separate
        // from this list projection to avoid an N+1 join on the list endpoint.

    /// <summary>
    /// Create payload (API Spec §4.6). <paramref name="TenantId"/> is used only by a system-admin
    /// caller to target a tenant; for a tenant caller it is ignored (the token's tenant is used).
    /// <para>
    /// <paramref name="Matica"/> / <paramref name="Evolis"/> (Printing Module, phase 7) let a
    /// system admin attach the product's print configuration in the same call — "the product and
    /// its printing configuration should behave as a single aggregate." Supplying either one
    /// requires a system-admin caller (decision Q-09, confirmed); a tenant caller supplying
    /// either fails with <c>PrintingErrors.ProductPrintConfigOnlySystemAdmin</c>. A tenant caller
    /// may still create a plain product with neither supplied — only the combined
    /// product-plus-configuration action is admin-gated, not product creation itself. Exactly one
    /// of the two, matching <see cref="UsingPrinterType"/>, is required when either is supplied —
    /// enforced by <c>IProductPrintConfigComposer.ValidateAsync</c>, the same validation the
    /// standalone <c>PUT /api/products/{id}/print-config</c> endpoint uses.
    /// </para>
    /// </summary>
    public sealed record CreateProductRequest(
        string Name,
        ProductTransactionWay ProductTransactionWay,
        UsingPrinterType UsingPrinterType,
        ActivationStatus ActivationStatus = DomainLayer.Enums.ActivationStatus.Active,
        int LowProductThreshold = 0,
        long? TenantId = null,
        MaticaPrintConfigRequest? Matica = null,
        EvolisPrintConfigRequest? Evolis = null);

    /// <summary>
    /// Update payload. A product cannot be reassigned to another tenant.
    /// <para>
    /// <b>Printing Module, phase 7:</b> no longer carries <c>UsingPrinterType</c> — a product's
    /// printer family can only be changed via <c>PUT /api/products/{id}/print-config</c>
    /// (system-admin only, confirmed), which switches it atomically together with the matching
    /// configuration row (decision Q-08). Letting this endpoint change it independently would let
    /// a product's declared family and its configuration row disagree with each other.
    /// </para>
    /// </summary>
    public sealed record UpdateProductRequest(
        string Name,
        ProductTransactionWay ProductTransactionWay,
        ActivationStatus ActivationStatus,
        int LowProductThreshold);

    /// <summary>
    /// Filter/paging inputs for <c>GET /api/products</c> (API Spec §3.1, §4.6). Bound from the query
    /// string. <paramref name="IsDeleted"/> is tri-state (null = both). <paramref name="TenantId"/>
    /// is honoured only for system-admin callers.
    /// </summary>
    /// <param name="Name">Optional case-insensitive name contains-filter.</param>
    /// <param name="ActivationStatus">Optional activation-state filter; null matches both.</param>
    /// <param name="ProductTransactionWay">Optional transaction-way filter; null matches both.</param>
    /// <param name="LowStockOnly">
    /// Optional low-stock filter (API Spec §4.6). Not applied yet — activated once the Stock
    /// aggregate (ERD §3.1) exists; the field is exposed now to keep the frontend contract stable.
    /// </param>
    /// <param name="IsDeleted">Optional soft-delete filter; null matches both.</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100 per spec). Defaults to 20.</param>
    /// <param name="SortBy">Optional sort field; mapped to a whitelisted column in the repository.</param>
    /// <param name="SortDir">Sort direction, "asc" or "desc". Defaults to "asc".</param>
    public sealed record ProductListFilter(
        string? Name = null,
        ActivationStatus? ActivationStatus = null,
        ProductTransactionWay? ProductTransactionWay = null,
        bool? LowStockOnly = null,
        bool? IsDeleted = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");
}
