using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Disposals;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Standalone card disposal endpoints (API §4.10, Addendum A). Writes off cards sitting at a
    /// branch — damaged, spoiled, or discontinued — outside of any transfer. Disposal that happens
    /// while settling a transfer is <c>TransactionsController</c>'s
    /// <see cref="TransactionsController.Receive"/> and
    /// <see cref="TransactionsController.Dispose"/>, not this controller.
    /// <para>
    /// Two resources share this controller under the common <c>api/inventory</c> prefix: writing a
    /// disposal is a card-level action (<c>cards/dispose</c>), while listing and reading disposals
    /// is its own resource (<c>disposals</c>). Routes are given in full on each action rather than
    /// composed from a single class-level prefix, since the two don't share a parent segment.
    /// </para>
    /// <b>Writing a disposal is never available to a system admin</b> — <see cref="Dispose"/>
    /// rejects an admin token outright, since disposal is a tenant-only concept end to end.
    /// <see cref="GetAll"/> and <see cref="GetById"/> are the exception: like
    /// <c>TransactionsController</c>'s reads, a system admin gets cross-tenant read access there
    /// (confirmed against <c>DisposalService.ResolveReadScope</c>, which returns no tenant filter
    /// for an admin caller) — a correction to this doc comment's earlier claim that no read-only
    /// admin path exists here at all.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs. <see cref="Dispose"/> can additionally return this code
    /// with the standard envelope (<c>Disposal.ActorNotResolved</c>) in the rare edge case where a
    /// token passes authentication but the acting tenant identity can't be resolved from it.
    /// </response>
    [ApiController]
    [Route("api/inventory")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class CardDisposalsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public CardDisposalsController(IServiceManager services) => _services = services;

        /// <summary>
        /// Writes off cards at a branch, named either by explicit id or by per-product quantity
        /// (FIFO selection). Requires a non-empty reason; rejects a system-admin caller outright.
        /// </summary>
        /// <response code="200">The disposal record, including every card written off under it.</response>
        /// <response code="403">A system-admin token attempted this — never permitted.</response>
        /// <response code="404">The disposing branch, or a named card, doesn't exist (or belongs to another tenant).</response>
        /// <response code="409">A named card is already disposed, already issued, or committed to an in-flight transfer; or the branch lacks enough available cards of a named product.</response>
        /// <response code="422">The request body failed validation — no reason, no cards identified, or both a card list and a quantity list supplied. See DisposalErrors.cs for the full catalogue.</response>
        [HttpPost("cards/dispose")]
        [ProducesResponseType(typeof(ApiResponse<DisposalDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Dispose([FromBody] DisposeCardsRequest request, CancellationToken cancellationToken)
            => (await _services.Disposals.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>Lists disposals with paging and filters.</summary>
        /// <response code="200">A page of disposals, scoped to the caller's tenant (or any tenant, for a system admin).</response>
        [HttpGet("disposals")]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<DisposalListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] DisposalListFilter filter, CancellationToken cancellationToken)
            => (await _services.Disposals.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets one disposal's full detail, including every card written off under it.</summary>
        /// <response code="200">The disposal.</response>
        /// <response code="404">No disposal exists with the supplied id.</response>
        [HttpGet("disposals/{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<DisposalDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Disposals.GetByIdAsync(id, cancellationToken)).ToActionResult(this);
    }
}
