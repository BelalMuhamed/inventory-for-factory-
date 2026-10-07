using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Authentication endpoints (API Spec §4.1), reconciled with the authoritative §2 model:
    /// single account per tenant, minimal JWT claims, no roles/permissions/branch.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class AuthController : ControllerBase
    {
        private readonly IServiceManager _services;
        private readonly ICurrentTenant _currentTenant;

        /// <summary>Creates the controller from the service façade and current-principal accessor.</summary>
        public AuthController(IServiceManager services, ICurrentTenant currentTenant)
        {
            _services = services;
            _currentTenant = currentTenant;
        }

        /// <summary>
        /// Authenticates a tenant by username and password and returns a JWT plus refresh token.
        /// </summary>
        /// <param name="request">Tenant credentials.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Authentication succeeded; returns the access/refresh token pair.</response>
        /// <response code="401">
        /// The username or password is incorrect, or the tenant is inactive
        /// (<c>Auth.InvalidCredentials</c>) — same code either way, so the response never reveals
        /// which. Returned by the service as the standard <see cref="ApiResponse{T}"/> envelope.
        /// </response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("tenant")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> LoginTenant(
            [FromBody] TenantLoginRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LoginTenantAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Authenticates the bootstrap system admin and returns a JWT (with <c>isSystemAdmin</c>)
        /// plus refresh token.
        /// </summary>
        /// <param name="request">Administrator credentials.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Authentication succeeded; returns the access/refresh token pair.</response>
        /// <response code="401">
        /// The username or password is incorrect, or the admin is inactive
        /// (<c>Auth.InvalidCredentials</c> — the same code <c>POST /api/auth/tenant</c> uses).
        /// Returned by the service as the standard <see cref="ApiResponse{T}"/> envelope.
        /// </response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("admin")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> LoginAdmin(
            [FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LoginSystemAdminAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Exchanges a valid refresh token for a new token pair, rotating the old token.</summary>
        /// <param name="request">The current refresh token.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Refresh succeeded; returns a new access/refresh token pair.</response>
        /// <response code="401">The refresh token is unknown, expired, or revoked.</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.RefreshAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Revokes the caller's refresh token.</summary>
        /// <param name="request">The refresh token to revoke.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Logout succeeded (idempotent); the payload is null.</response>
        /// <response code="401">
        /// No valid bearer token was supplied. Returned by the authentication middleware before
        /// this action runs — the response body is empty (no <see cref="ApiResponse{T}"/>
        /// envelope), since <c>LogoutAsync</c> itself never fails.
        /// </response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Logout(
            [FromBody] LogoutRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.LogoutAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Returns the current principal (username, admin flag) to bootstrap UI state.
        /// </summary>
        /// <response code="200">The authenticated principal.</response>
        /// <response code="401">
        /// No valid bearer token was supplied. Returned by the authentication middleware before
        /// this action runs — the response body is empty (no <see cref="ApiResponse{T}"/> envelope).
        /// </response>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<CurrentPrincipalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Me()
        {
            var profile = new CurrentPrincipalResponse(_currentTenant.Username, _currentTenant.IsSystemAdmin);
            return Result.Success(profile).ToActionResult(this);
        }

        /// <summary>
        /// Mints a short-lived, narrowly-scoped token for the Matica Printer Agent (Matica Print
        /// Flow). The caller — Angular, holding its own normal session token — supplies the branch
        /// and printer the Printer Agent will operate for; both are validated as belonging to the
        /// caller's own tenant before a token is issued. The Printer Agent then uses the returned
        /// token, not the caller's real bearer token, for its own two backend calls.
        /// </summary>
        /// <param name="request">The target branch and printer.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Token minted; returns the signed access token and its (short) expiry.</response>
        /// <response code="403">The caller is a system admin (no tenant context to scope the token to).</response>
        /// <response code="404">The supplied branch or printer does not exist, or does not belong to the caller's tenant.</response>
        [HttpPost("print-agent-token")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<PrintAgentTokenResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreatePrintAgentToken(
            [FromBody] CreatePrintAgentTokenRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.CreatePrintAgentTokenAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Exchanges a reconciliation service account's client id/secret for a short-lived
        /// service token (Matica Print Flow, reconciliation-credential phase). Client-credentials
        /// style — no user session involved, unlike every other endpoint in this controller except
        /// login itself. Called by the Printer Agent's background outbox reconciliation job, never
        /// during a live print request.
        /// </summary>
        /// <param name="request">The service account's client id and secret.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Token minted; returns the signed access token and its (short) expiry.</response>
        /// <response code="401">The client id/secret is invalid, or the account has been revoked.</response>
        [HttpPost("service-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<ServiceTokenResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateServiceToken(
            [FromBody] ServiceTokenRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.CreateServiceTokenAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Provisions a new reconciliation service account for one Printer Agent instance
        /// (Matica Print Flow, reconciliation-credential phase). System-admin only — provisioning
        /// a standing credential is a more consequential operation than a tenant self-serving a
        /// short-lived, already-scoped print-agent token.
        /// </summary>
        /// <param name="request">The owning tenant, branch, and a human-readable label.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">
        /// Account provisioned. <c>ClientSecret</c> in the response is shown exactly once — only
        /// its hash is persisted, so this is the only opportunity to record it.
        /// </response>
        /// <response code="404">The supplied branch does not exist, or does not belong to the supplied tenant.</response>
        [HttpPost("service-accounts")]
        [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
        [ProducesResponseType(typeof(ApiResponse<CreateServiceAccountResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateServiceAccount(
            [FromBody] CreateServiceAccountRequest request, CancellationToken cancellationToken)
            => (await _services.Auth.CreateServiceAccountAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Revokes a reconciliation service account (Matica Print Flow, reconciliation-credential
        /// phase). System-admin only. Idempotent — revoking an already-revoked account still
        /// succeeds. Takes effect immediately for new token-mint attempts; an already-minted token
        /// remains valid until it naturally expires (a few minutes).
        /// </summary>
        /// <param name="id">The service account's id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">Revoked (or already was).</response>
        /// <response code="404">No service account exists with the supplied id.</response>
        [HttpPost("service-accounts/{id:long}/revoke")]
        [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeServiceAccount(long id, CancellationToken cancellationToken)
            => (await _services.Auth.RevokeServiceAccountAsync(id, cancellationToken)).ToActionResult(this);
    }
}
