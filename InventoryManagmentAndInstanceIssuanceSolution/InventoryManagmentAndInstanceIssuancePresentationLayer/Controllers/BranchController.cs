using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Branches;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Branch management endpoints (API Spec §4.5). Requires authentication: tenant callers manage
    /// their own branches; a system admin manages any tenant's branches. Hard delete is not exposed.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs. <c>Create</c>, <c>Delete</c>, and <c>Restore</c> can
    /// additionally return this code with the standard envelope
    /// (<c>Branch.ActorNotResolved</c>) in the rare edge case where a token passes authentication
    /// but the acting tenant/admin identity can't be resolved from it — see <c>Create</c>'s own
    /// example for that shape.
    /// </response>
    [ApiController]
    [Route("api/branches")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class BranchController : ControllerBase
    {
        private readonly IServiceManager _services;

        public BranchController(IServiceManager services) => _services = services;

        /// <summary>Lists branches with paging and filters.</summary>
        /// <response code="200">A page of branches.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<BranchResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] BranchListFilter filter, CancellationToken cancellationToken)
            => (await _services.Branches.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets a branch by id.</summary>
        /// <response code="200">The branch.</response>
        /// <response code="404">No branch exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<BranchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Branches.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Creates a branch. Admin callers supply the target tenant id.</summary>
        /// <response code="200">The created branch.</response>
        /// <response code="409">A branch with this name already exists for the tenant.</response>
        /// <response code="422">The request body failed validation, or (system-admin caller) the target tenant id is missing or doesn't exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<BranchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
            => (await _services.Branches.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Updates a branch's name, location, and active state.</summary>
        /// <response code="200">The updated branch.</response>
        /// <response code="404">No branch exists with the supplied id.</response>
        /// <response code="409">The new name is already used by another branch for this tenant.</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<BranchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
            => (await _services.Branches.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Soft-deletes a branch.</summary>
        /// <response code="200">The branch was soft-deleted; the payload is null.</response>
        /// <response code="404">No branch exists with the supplied id.</response>
        /// <response code="409">The branch is already deleted, holds non-zero stock, has an in-progress transfer, or has an open stock request — see BranchErrors for the specific code.</response>
        [HttpDelete("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
            => (await _services.Branches.SoftDeleteAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Restores a soft-deleted branch.</summary>
        /// <response code="200">The branch was restored; the payload is null.</response>
        /// <response code="404">No branch exists with the supplied id.</response>
        /// <response code="409">The branch is not currently deleted.</response>
        [HttpPost("{id:long}/restore")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken)
            => (await _services.Branches.RestoreAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Activates a branch (idempotent).</summary>
        /// <response code="200">The branch is now active.</response>
        /// <response code="404">No branch exists with the supplied id.</response>
        [HttpPost("{id:long}/activate")]
        [ProducesResponseType(typeof(ApiResponse<BranchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
            => (await _services.Branches.ActivateAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Deactivates a branch (idempotent).</summary>
        /// <response code="200">The branch is now inactive.</response>
        /// <response code="404">No branch exists with the supplied id.</response>
        [HttpPost("{id:long}/deactivate")]
        [ProducesResponseType(typeof(ApiResponse<BranchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
            => (await _services.Branches.DeactivateAsync(id, cancellationToken)).ToActionResult(this);
    }
}