// InventoryManagmentAndInstanceIssuancePresentationLayer/Controllers/TenantsController.cs
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Tenants;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Tenant management endpoints (API Spec §4.2). Restricted to the bootstrap system admin via
    /// the <see cref="AuthorizationPolicies.SystemAdminOnly"/> policy. Hard delete is intentionally
    /// not exposed; only soft delete and restore are available.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. On every action here this is the authorization
    /// middleware rejecting the request before it runs — the body is empty, not the standard
    /// <see cref="ApiResponse{T}"/> envelope. The sole exception is <c>DELETE /{id}</c>, which can
    /// additionally return this code <em>with</em> the envelope in one rare edge case — see that
    /// action's own documentation.
    /// </response>
    /// <response code="403">
    /// The token is valid but is not a system-admin token. Always the authorization middleware's
    /// empty-body rejection — no action in this controller returns a Forbidden result itself.
    /// </response>
    [ApiController]
    [Route("api/tenants")]
    [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class TenantsController : ControllerBase
    {
        private readonly IServiceManager _services;
        private readonly ICurrentTenant _currentTenant;

        /// <summary>Creates the controller from the service façade and current-principal accessor.</summary>
        public TenantsController(IServiceManager services, ICurrentTenant currentTenant)
        {
            _services = services;
            _currentTenant = currentTenant;
        }

        /// <summary>Lists tenants with paging and filters (name, isActive, isDeleted).</summary>
        /// <param name="filter">Filter, paging, and sort inputs bound from the query string.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">A page of tenants.</response>
        /// <response code="422">A query parameter could not be bound.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<TenantResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> GetAll(
            [FromQuery] TenantListFilter filter, CancellationToken cancellationToken)
            => (await _services.Tenants.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets a single tenant by id, including soft-deleted tenants.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The tenant.</response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<TenantResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Tenants.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Creates a new tenant.</summary>
        /// <param name="request">Creation payload.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The created tenant.</response>
        /// <response code="409">The code or username is already in use (including by a soft-deleted tenant).</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<TenantResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create(
            [FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
            => (await _services.Tenants.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Updates a tenant's username, code, and active state.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="request">Update payload.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The updated tenant.</response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        /// <response code="409">The code or username is already in use, or a concurrent update was detected.</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<TenantResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(
            long id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
            => (await _services.Tenants.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Changes a tenant's password.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="request">New-password payload.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The password was changed; the payload is null.</response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPut("{id:long}/password")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ChangePassword(
            long id, [FromBody] ChangeTenantPasswordRequest request, CancellationToken cancellationToken)
            => (await _services.Tenants.ChangePasswordAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Soft-deletes a tenant, recording the acting admin as the deleter.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The tenant was soft-deleted; the payload is null.</response>
        /// <response code="401">
        /// Beyond the usual empty-body middleware rejection (see the controller-level 401 doc),
        /// this action can also return 401 <em>with</em> the standard envelope
        /// (<c>Tenant.ActorNotResolved</c>) in the edge case where the bearer token passed the
        /// system-admin policy check but the acting admin's identity could not be resolved from
        /// it — a defensive check, not an expected client-facing scenario.
        /// </response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        /// <response code="409">The tenant is already deleted.</response>
        [HttpDelete("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SoftDelete(long id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTenant.Username))
            {
                return Result.Failure(TenantErrors.ActorNotResolved()).ToActionResult(this);
            }

            return (await _services.Tenants.SoftDeleteAsync(id, _currentTenant.Username, cancellationToken))
                .ToActionResult(this);
        }

        /// <summary>Restores a soft-deleted tenant.</summary>
        /// <param name="id">Tenant id.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The tenant was restored; the payload is null.</response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        /// <response code="409">The tenant is not currently deleted.</response>
        [HttpPost("{id:long}/restore")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken)
            => (await _services.Tenants.RestoreAsync(id, cancellationToken)).ToActionResult(this);
    }
}
