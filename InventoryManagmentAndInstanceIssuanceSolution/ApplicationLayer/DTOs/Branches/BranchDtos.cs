using System;

namespace ApplicationLayer.DTOs.Branches
{
    /// <summary>Branch projection returned to clients (no entity leaks past Application).</summary>
    public sealed record BranchResponse(
        long Id,
        long TenantId,
        string Name,
        string? Location,
        bool IsActive,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        DateTime? DeletedAt);

    /// <summary>
    /// Create payload (API Spec §4.5). <paramref name="TenantId"/> is used only by a system-admin
    /// caller to target a tenant; for a tenant caller it is ignored (the token's tenant is used).
    /// </summary>
    public sealed record CreateBranchRequest(
        string Name,
        string? Location,
        bool IsActive = true,
        long? TenantId = null);

    /// <summary>Update payload. A branch cannot be reassigned to another tenant.</summary>
    public sealed record UpdateBranchRequest(
        string Name,
        string? Location,
        bool IsActive);

    /// <summary>
    /// Filter/paging inputs for <c>GET /api/branches</c>. <paramref name="IsDeleted"/> is tri-state
    /// (null = both). <paramref name="TenantId"/> is honoured only for system-admin callers.
    /// </summary>
    public sealed record BranchListFilter(
        string? Name = null,
        bool? IsActive = null,
        bool? IsDeleted = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");
}