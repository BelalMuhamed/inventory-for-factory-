// ApplicationLayer/Contracts/ICurrentTenant.cs
namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Ambient accessor for the authenticated principal, resolved from the current request's JWT
    /// claims. <see cref="UserId"/> carries whichever identity applies — the tenant id for a tenant
    /// token, or the system-admin id for an admin token — disambiguated by <see cref="IsSystemAdmin"/>.
    /// <para>
    /// On unauthenticated paths (e.g. login) no principal is present: <see cref="UserId"/> is
    /// <c>null</c> and <see cref="IsSystemAdmin"/> is <c>false</c>.
    /// </para>
    /// </summary>
    public interface ICurrentTenant
    {
        /// <summary>Authenticated principal's username, or <c>null</c> when unauthenticated.</summary>
        string? Username { get; }

        /// <summary>True when the caller presented a system-admin token (bypasses tenant filters).</summary>
        bool IsSystemAdmin { get; }
        /// <summary>Authenticated tenant's id, or <c>null</c> for a system admin (no tenant context).</summary>
        long? TenantId { get; }
    }
}