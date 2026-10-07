using DomainLayer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Stocks
{

    public sealed record ProductStockResponse(long ProductId, string ProductName, int AvaliableQuantity, int HoldQuantity, byte[] RowVersion, DateTime UpdatedAt);
    /// <summary>
    /// Flat stock row (Product × Branch) returned by <c>GET /api/stock</c> and
    /// <c>GET /api/stock/branches/{branchId}</c> (API Spec §4.7). <see cref="IsLow"/> is
    /// <c>AvailableQuantity &lt;= LowProductThreshold</c> (ERD §3.1). <see cref="RowVersion"/> is the
    /// base64 optimistic-concurrency token.
    /// </summary>
    public sealed record StockRowResponse(
        long TenantId,
        long BranchId,
        string BranchName,
        long ProductId,
        string ProductName,
        int AvailableQuantity,
        int HoldQuantity,
        int LowProductThreshold,
        bool IsLow,
        string RowVersion,
        DateTime UpdatedAt);

    /// <summary>
    /// Filter/paging inputs for the stock list endpoints (API Spec §3.1, §4.7). Bound from the query
    /// string. <paramref name="TenantId"/> is honoured only for system-admin callers.
    /// </summary>
    /// <param name="ProductId">Optional product filter.</param>
    /// <param name="BranchId">Optional branch filter (forced by the branch-scoped endpoint).</param>
    /// <param name="LowStockOnly">When true, keeps rows where AvailableQuantity &lt;= LowProductThreshold.</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100). Defaults to 20.</param>
    /// <param name="SortBy">available | hold | branchid | productid | updatedat (default).</param>
    /// <param name="SortDir">asc (default) or desc.</param>
    public sealed record StockListFilter(
        long? ProductId = null,
        long? BranchId = null,
        bool? LowStockOnly = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");


}
