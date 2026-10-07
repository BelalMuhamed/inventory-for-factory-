using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Auth;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Authentication use cases (API Spec §4.1). Every operation returns a <see cref="Result"/>
    /// outcome; invalid credentials and token failures surface as
    /// <see cref="ErrorCategory.Unauthorized"/> rather than exceptions.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Authenticates a tenant by username and password and issues an access/refresh token pair.
        /// Logs a <c>Login</c> audit entry on success.
        /// </summary>
        /// <param name="request">The tenant credentials.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The token pair on success; an <see cref="ErrorCategory.Unauthorized"/> error otherwise.</returns>
        Task<Result<AuthResponse>> LoginTenantAsync(TenantLoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Authenticates the bootstrap system admin and issues an access/refresh token pair
        /// carrying <c>isSystemAdmin = true</c>.
        /// </summary>
        /// <param name="request">The administrator credentials.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<AuthResponse>> LoginSystemAdminAsync(AdminLoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exchanges a valid refresh token for a new token pair, rotating (revoking) the old token.
        /// A revoked, expired, or unknown token yields an <see cref="ErrorCategory.Unauthorized"/> error.
        /// </summary>
        /// <param name="request">The refresh request carrying the current refresh token.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes the caller's refresh token. Idempotent: revoking an already-revoked or unknown
        /// token still succeeds so logout cannot be probed for token validity.
        /// </summary>
        /// <param name="request">The logout request carrying the refresh token to revoke.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mints a short-lived, narrowly-scoped token for the Matica Printer Agent (Matica Print
        /// Flow), after confirming the target branch and printer both belong to the caller's own
        /// tenant. Requires a tenant token — a system-admin caller has no tenant context to scope
        /// the token to, and is rejected outright.
        /// </summary>
        /// <param name="request">The target branch and printer the Printer Agent will operate for.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<PrintAgentTokenResponse>> CreatePrintAgentTokenAsync(
            CreatePrintAgentTokenRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exchanges a service account's client id/secret for a short-lived reconciliation access
        /// token. Client-credentials style — no user session involved. Returns the same
        /// <see cref="AuthErrors.ServiceCredentialInvalid"/> code whether the client id doesn't
        /// exist or the secret is wrong, and a distinct <see cref="AuthErrors.ServiceCredentialRevoked"/>
        /// when the account exists and the secret matches but has been revoked.
        /// </summary>
        /// <param name="request">The client id and secret.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<ServiceTokenResponse>> CreateServiceTokenAsync(
            ServiceTokenRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Provisions a new reconciliation service account for one Printer Agent instance
        /// (system-admin only). The returned <see cref="CreateServiceAccountResponse.ClientSecret"/>
        /// is shown exactly once; only its hash is persisted.
        /// </summary>
        /// <param name="request">The owning tenant, branch, and a human-readable label.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<CreateServiceAccountResponse>> CreateServiceAccountAsync(
            CreateServiceAccountRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes a reconciliation service account (system-admin only). Idempotent: revoking an
        /// already-revoked account still succeeds.
        /// </summary>
        /// <param name="id">The service account's id.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> RevokeServiceAccountAsync(long id, CancellationToken cancellationToken = default);
    }
}
