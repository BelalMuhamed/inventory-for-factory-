using System;

namespace ApplicationLayer.DTOs.Auth
{
    /// <summary>Credentials for tenant authentication at <c>POST /api/auth/tenant</c>.</summary>
    /// <param name="Username">Tenant login username (equals the tenant name).</param>
    /// <param name="Password">Plaintext password; verified against the stored hash and never logged.</param>
    public sealed record TenantLoginRequest(string Username, string Password);

    /// <summary>Credentials for system-admin authentication at <c>POST /api/auth/admin</c>.</summary>
    /// <param name="Username">Administrator login username.</param>
    /// <param name="Password">Plaintext password; verified against the stored hash and never logged.</param>
    public sealed record AdminLoginRequest(string Username, string Password);

    /// <summary>Payload for <c>POST /api/auth/refresh</c>.</summary>
    /// <param name="RefreshToken">The opaque refresh token previously issued to the caller.</param>
    public sealed record RefreshRequest(string RefreshToken);

    /// <summary>Payload for <c>POST /api/auth/logout</c>.</summary>
    /// <param name="RefreshToken">The refresh token to revoke.</param>
    public sealed record LogoutRequest(string RefreshToken);

    /// <summary>
    /// Successful authentication result returned by login and refresh. Carries the short-lived
    /// access token and its expiry alongside the opaque refresh token used to obtain the next one.
    /// </summary>
    /// <param name="AccessToken">Signed JWT to present as <c>Authorization: Bearer</c>.</param>
    /// <param name="AccessTokenExpiresAt">UTC expiry of the access token.</param>
    /// <param name="RefreshToken">Opaque refresh token (returned once; only its hash is stored).</param>
    /// <param name="RefreshTokenExpiresAt">UTC expiry of the refresh token.</param>
    public sealed record AuthResponse(
        string AccessToken,
        DateTime AccessTokenExpiresAt, 
        string RefreshToken,
        DateTime RefreshTokenExpiresAt);

    /// <summary>
    /// Current-principal projection returned by <c>GET /api/auth/me</c>, used by clients to
    /// bootstrap UI state. Reflects only the claims the spec permits (no role/permissions/branch).
    /// Carries no tenant id — a caller who needs it decodes the JWT's own <c>tenantId</c> claim.
    /// </summary>
    /// <param name="Username">Login username of the authenticated principal.</param>
    /// <param name="IsSystemAdmin">True when the caller is the bootstrap system admin.</param>
    public sealed record CurrentPrincipalResponse(string Username, bool IsSystemAdmin);

    /// <summary>
    /// Payload for <c>POST /api/auth/print-agent-token</c> (Matica Print Flow). Both ids are
    /// validated as belonging to the caller's own tenant before a token is minted.
    /// </summary>
    /// <param name="BranchId">Branch the Printer Agent will operate at for this print session.</param>
    /// <param name="PrinterId">Printer the Printer Agent will drive for this print session.</param>
    public sealed record CreatePrintAgentTokenRequest(long BranchId, long PrinterId);

    /// <summary>
    /// Short-lived, narrowly-scoped token for the Matica Printer Agent, returned by
    /// <c>POST /api/auth/print-agent-token</c>. Signed with a dedicated key
    /// (see <c>PrintAgentTokenOptions</c>) distinct from the caller's own session token — the
    /// Printer Agent never sees, and never needs, the caller's real bearer token.
    /// </summary>
    /// <param name="AccessToken">Signed JWT to present as <c>Authorization: Bearer</c> to the print-flow endpoints.</param>
    /// <param name="ExpiresAt">UTC expiry — deliberately short-lived (see <c>PrintAgentTokenOptions.AccessTokenMinutes</c>).</param>
    public sealed record PrintAgentTokenResponse(string AccessToken, DateTime ExpiresAt);

    /// <summary>
    /// Payload for <c>POST /api/auth/service-token</c> (Matica Print Flow, reconciliation
    /// credential). Client-credentials style: no user session involved, the caller authenticates
    /// entirely with its own standing <see cref="DomainLayer.Entities.PrintAgentServiceAccount"/>.
    /// </summary>
    /// <param name="ClientId">The service account's public identifier.</param>
    /// <param name="ClientSecret">The service account's secret, verified against its stored hash and never logged.</param>
    public sealed record ServiceTokenRequest(Guid ClientId, string ClientSecret);

    /// <summary>
    /// Short-lived token for the Matica Printer Agent's background reconciliation job, returned by
    /// <c>POST /api/auth/service-token</c>. Signed with a third dedicated key
    /// (see <c>ReconciliationTokenOptions</c>), distinct from both the tenant/admin key and the
    /// print-agent-token key.
    /// </summary>
    /// <param name="AccessToken">Signed JWT to present as <c>Authorization: Bearer</c> to <c>print-result</c>.</param>
    /// <param name="ExpiresAt">UTC expiry.</param>
    public sealed record ServiceTokenResponse(string AccessToken, DateTime ExpiresAt);

    /// <summary>
    /// Payload for <c>POST /api/auth/service-accounts</c> (system-admin only) — provisions a new
    /// reconciliation service account for one Printer Agent instance.
    /// </summary>
    /// <param name="TenantId">Owning tenant.</param>
    /// <param name="BranchId">The single branch this service account is scoped to.</param>
    /// <param name="Label">Human-readable label for operators (e.g. "Branch 12 Printer Agent").</param>
    public sealed record CreateServiceAccountRequest(long TenantId, long BranchId, string Label);

    /// <summary>
    /// Result of provisioning a service account. <see cref="ClientSecret"/> is the raw secret,
    /// shown exactly once — only its hash is persisted, so this is the only time it is ever
    /// retrievable. Losing it means provisioning a new account; there is no recovery.
    /// </summary>
    /// <param name="Id">The service account's id, used to revoke it later.</param>
    /// <param name="ClientId">The public identifier to configure into the Printer Agent.</param>
    /// <param name="ClientSecret">The raw secret — shown once, never stored, never retrievable again.</param>
    /// <param name="Label">Echoes the label supplied at creation.</param>
    public sealed record CreateServiceAccountResponse(long Id, Guid ClientId, string ClientSecret, string Label);
}
