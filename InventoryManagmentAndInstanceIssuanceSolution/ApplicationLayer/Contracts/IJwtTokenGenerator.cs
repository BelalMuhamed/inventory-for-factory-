using System;

namespace ApplicationLayer.Contracts
{
    /// <summary>A signed access token together with its UTC expiry.</summary>
    /// <param name="Token">The serialized, signed JWT.</param>
    /// <param name="ExpiresAt">UTC instant at which the token expires.</param>
    public readonly record struct AccessToken(string Token, DateTime ExpiresAt);

    /// <summary>
    /// Issues signed JWT access tokens. Per API Spec §2, tenant tokens carry only
    /// <c>sub</c> and <c>tenantId</c>; the system-admin token additionally carries
    /// <c>isSystemAdmin = true</c>. No role, permissions, or branch claims are ever emitted.
    /// </summary>
    /// <summary>
    /// Issues signed JWT access tokens. A tenant token carries <c>username</c>, <c>tenantId</c>,
    /// and <c>isSystemAdmin = false</c>; the system-admin token carries <c>username</c> and
    /// <c>isSystemAdmin = true</c> (no <c>tenantId</c>). No role, permissions, or branch claims are emitted.
    /// </summary>
    public interface IJwtTokenGenerator
    {
        /// <summary>Creates an access token for an authenticated tenant.</summary>
        /// <param name="username">The tenant's login username (becomes the <c>username</c> claim).</param>
        /// <param name="tenantId">The tenant's id (becomes the <c>tenantId</c> claim).</param>
        AccessToken CreateForTenant(string username, long tenantId);

        /// <summary>Creates an access token for the bootstrap system admin (no <c>tenantId</c> claim).</summary>
        /// <param name="username">The administrator's login username.</param>
        AccessToken CreateForSystemAdmin(string username);
    }
}
