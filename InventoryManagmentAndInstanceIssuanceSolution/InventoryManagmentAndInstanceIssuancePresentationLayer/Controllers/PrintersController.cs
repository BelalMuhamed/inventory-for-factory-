using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Printer registry endpoints (ERD §6, Printing Module decisions Q-01/Q-09). Requires
    /// authentication: tenant callers get read-only access, scoped to their own tenant; only a
    /// system admin may create, update, delete, or restore a printer —
    /// <see cref="IPrinterConfigurationService"/> enforces this itself, not this controller,
    /// matching the locked "[Authorize] only, no permission attributes" convention. Hard delete
    /// is not exposed.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs.
    /// </response>
    /// <response code="403">
    /// A tenant caller attempted a write action (Create/Update/Delete/Restore). Unlike
    /// <c>TenantsController</c>'s 403 (an authorization-policy rejection), this one is enforced in
    /// the service and returned as the standard <see cref="ApiResponse{T}"/> envelope
    /// (<c>Printer.OnlySystemAdmin</c>) — see each write action's example.
    /// </response>
    [ApiController]
    [Route("api/printers")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class PrintersController : ControllerBase
    {
        private readonly IServiceManager _services;

        public PrintersController(IServiceManager services) => _services = services;

        /// <summary>Lists printers with paging and filters (type, branch).</summary>
        /// <response code="200">A page of printers.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<PrinterResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] PrinterListFilter filter, CancellationToken cancellationToken)
            => (await _services.Printers.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets a printer by id.</summary>
        /// <response code="200">The printer.</response>
        /// <response code="404">No printer exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<PrinterResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Printers.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Registers a new printer. System-admin only; admin callers supply the target tenant id.</summary>
        /// <response code="200">The registered printer.</response>
        /// <response code="403">A tenant caller (not a system admin) attempted this.</response>
        /// <response code="409">Another non-deleted printer for this tenant already has this serial/IP.</response>
        /// <response code="422">The request body failed validation (e.g. a Matica printer missing its machine configuration, or an Evolis printer supplying one).</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PrinterResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreatePrinterRequest request, CancellationToken cancellationToken)
            => (await _services.Printers.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Updates a printer's branch, name, model, unique number, and Matica machine configuration. System-admin only.</summary>
        /// <response code="200">The updated printer.</response>
        /// <response code="403">A tenant caller attempted this.</response>
        /// <response code="404">No printer exists with the supplied id.</response>
        /// <response code="409">The new serial/IP is already used by another printer for this tenant.</response>
        /// <response code="422">The request body failed validation (e.g. the target branch is soft-deleted).</response>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<PrinterResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePrinterRequest request, CancellationToken cancellationToken)
            => (await _services.Printers.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Soft-deletes a printer. System-admin only.</summary>
        /// <response code="200">The printer was soft-deleted; the payload is null.</response>
        /// <response code="403">A tenant caller attempted this.</response>
        /// <response code="404">No printer exists with the supplied id.</response>
        /// <response code="409">The printer is already deleted.</response>
        [HttpDelete("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
            => (await _services.Printers.SoftDeleteAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Restores a soft-deleted printer. System-admin only.</summary>
        /// <response code="200">The printer was restored; the payload is null.</response>
        /// <response code="403">A tenant caller attempted this.</response>
        /// <response code="404">No printer exists with the supplied id.</response>
        /// <response code="409">The printer is not currently deleted.</response>
        [HttpPost("{id:long}/restore")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken)
            => (await _services.Printers.RestoreAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Supervisor self-registration (Evolis Integration Plan §7, decisions Q-21/Q-28). Called
        /// only by the Printer Agent's background reconciliation service-account token — never by
        /// Angular or a tenant/admin session, which is why this action has its own
        /// <see cref="AuthorizationPolicies.ReconciliationOnly"/> policy rather than
        /// <see cref="Authorize"/> at the class level (the rest of this controller's actions are
        /// system-admin/tenant-session only). Reconciles the Evolis devices the Agent actually
        /// discovered on its supervising machine against this branch's registered printers.
        /// </summary>
        /// <response code="200">The reconciliation result: matched, unmatched, and registered-but-not-discovered devices.</response>
        /// <response code="403">The request's <c>BranchId</c> does not match the authenticated service account's own branch.</response>
        /// <response code="422">The request body failed validation (e.g. a missing host name).</response>
        [HttpPost("supervisor-registration")]
        [Authorize(Policy = AuthorizationPolicies.ReconciliationOnly)]
        [ProducesResponseType(typeof(ApiResponse<SupervisorRegistrationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> RegisterSupervisor(
            [FromBody] SupervisorRegistrationRequest request, CancellationToken cancellationToken)
        {
            if (IsOutsideTokenScope(request.BranchId))
            {
                return Result.Failure<SupervisorRegistrationResponse>(PrintingErrors.SupervisorBranchMismatch())
                    .ToActionResult(this);
            }

            return (await _services.Printers.RegisterSupervisorAsync(request, cancellationToken)).ToActionResult(this);
        }

        /// <summary>
        /// Defense in depth, same reasoning and same claim as <c>PrintFlowController.IsOutsideTokenScope</c>:
        /// the reconciliation service-account token already scopes its holder to one branch via a
        /// <c>branchId</c> claim set once at mint time (<c>AuthController.CreateServiceToken</c>,
        /// after validating tenant ownership), so this just confirms the request body cannot
        /// silently disagree with it.
        /// </summary>
        private bool IsOutsideTokenScope(long requestedBranchId) =>
            User.FindFirstValue(ReconciliationTokenGenerator.BranchIdClaim) != requestedBranchId.ToString();
    }
}
