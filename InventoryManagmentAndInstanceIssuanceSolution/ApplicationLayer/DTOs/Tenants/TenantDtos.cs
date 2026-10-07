// ApplicationLayer/DTOs/Tenants/TenantDtos.cs
using System;

namespace ApplicationLayer.DTOs.Tenants
{
    /// <summary>
    /// Request body for <c>POST /api/tenants</c> (API Spec §4.2).
    /// </summary>
    /// <param name="Username">Unique login/display identity (≤100 chars). Unique across all tenants, including deleted.</param>
    /// <param name="Code">Unique, URL-safe slug (≤50 chars). Unique across all tenants, including deleted.</param>
    /// <param name="Password">Plaintext password; hashed by the service and never stored or logged.</param>
    /// <param name="IsActive">Whether the tenant is active on creation. Defaults to active.</param>
    public sealed record CreateTenantRequest(string Username, string Code, string Password, bool IsActive = true);

    /// <summary>
    /// Request body for <c>PUT /api/tenants/{id}</c> (API Spec §4.2). Updates the mutable profile
    /// fields only; the password is changed through the dedicated password endpoint.
    /// </summary>
    /// <param name="Username">New unique login/display identity.</param>
    /// <param name="Code">New unique slug.</param>
    /// <param name="IsActive">New active state.</param>
    public sealed record UpdateTenantRequest(string Username, string Code, bool IsActive);

    /// <summary>Request body for <c>PUT /api/tenants/{id}/password</c>.</summary>
    /// <param name="NewPassword">New plaintext password; hashed by the service and never logged.</param>
    public sealed record ChangeTenantPasswordRequest(string NewPassword);

    /// <summary>
    /// Tenant projection returned by <c>GET /api/tenants/{id}</c> and as the element type of the
    /// list endpoint. Never exposes the password hash. Soft-delete fields are included so callers
    /// can distinguish active from deleted tenants (deleted tenants are returned on request).
    /// </summary>
    /// <param name="Id">Tenant primary key.</param>
    /// <param name="Username">Login/display identity.</param>
    /// <param name="Code">Unique slug.</param>
    /// <param name="IsActive">Whether the tenant is active.</param>
    /// <param name="IsDeleted">Whether the tenant is soft-deleted.</param>
    /// <param name="CreatedAt">UTC creation instant.</param>
    /// <param name="UpdatedAt">UTC instant of the last update, or <c>null</c>.</param>
    /// <param name="DeletedAt">UTC soft-delete instant, or <c>null</c>.</param>
    public sealed record TenantResponse(
        long Id,
        string Username,
        string Code,
        bool IsActive,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        DateTime? DeletedAt);

    /// <summary>
    /// Filter and paging inputs for <c>GET /api/tenants</c> (API Spec §3.1, §4.2). Bound from the
    /// query string. <paramref name="IsDeleted"/> is tri-state: <c>null</c> returns both active and
    /// deleted tenants, <c>true</c> only deleted, <c>false</c> only active.
    /// </summary>
    /// <param name="Username">Optional case-insensitive username contains-filter.</param>
    /// <param name="IsActive">Optional active-state filter; <c>null</c> matches both.</param>
    /// <param name="IsDeleted">Optional soft-delete filter; <c>null</c> matches both.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100 per spec). Defaults to 20.</param>
    /// <param name="SortBy">Optional sort field; mapped to a whitelisted column in the repository.</param>
    /// <param name="SortDir">Sort direction, "asc" or "desc". Defaults to "asc".</param>
    public sealed record TenantListFilter(
        string? Username = null,
        bool? IsActive = null,
        bool? IsDeleted = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");
}