using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.BranchRequests;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Branch stock requests endpoints (API §4.9). Requires authentication. A tenant caller creates,
    /// confirms, refuses, and cancels its own tenant's requests; a system admin has read-only access
    /// across tenants (decision Q7) — create, confirm, refuse, and cancel all reject an admin token outright.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs (<c>BranchRequest.ActorNotResolved</c> exists in the
    /// catalogue but, per its own doc comment, is unreachable behind <c>[Authorize]</c> with a
    /// valid tenant token — same as <c>Transfer.ActorNotResolved</c> in S4).
    /// </response>
    [ApiController]
    [Route("api/inventory/requests")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class BranchRequestsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public BranchRequestsController(IServiceManager services) => _services = services;

        /// <summary>Lists branch stock requests with paging and filters.</summary>
        /// <response code="200">A page of branch requests, scoped to the caller's tenant (or any tenant, for a system admin).</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<StockRequestListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] StockRequestListFilter filter, CancellationToken cancellationToken)
            => (await _services.BranchRequests.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets one branch stock request's full detail, including items, fulfilment counters, and linked transfers.</summary>
        /// <response code="200">The request, including any products dispatched but never asked for (unrequestedProducts).</response>
        /// <response code="404">No branch request exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.BranchRequests.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Creates a new branch stock request for a tenant-owned active branch.
        /// </summary>
        /// <response code="200">The created request — opens InProgress; reserves nothing and moves no stock.</response>
        /// <response code="403">A system-admin token attempted this — admin access to this module is read-only (decision Q7).</response>
        /// <response code="404">The requesting branch doesn't exist.</response>
        /// <response code="409">The requesting branch already has a non-terminal request covering one of the named products (decision Q-11 / D-08).</response>
        /// <response code="422">The request body failed validation, or the requesting branch is inactive (a request from it could never be confirmed, so creation fails early).</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreateStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Confirms a branch stock request, generating one or more inventory transfers to fulfil it.
        /// </summary>
        /// <response code="200">Confirm succeeded — carries every generated transfer's full detail, not just its id. Callable repeatedly against the same request from any non-terminal status (decision Q-04, incremental fulfilment).</response>
        /// <response code="403">A system-admin token attempted this.</response>
        /// <response code="404">No branch request exists with the supplied id.</response>
        /// <response code="409">The request is already Fulfilled, Refused, or Cancelled.</response>
        /// <response code="422">The request body failed validation — no transfer plans, a plan's source is the request's own requesting branch, or the generated transfer itself fails validation (reuses TransferErrors — see TransactionsController.Create).</response>
        [HttpPost("{id:long}/confirm")]
        [ProducesResponseType(typeof(ApiResponse<ConfirmStockRequestResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Confirm(
            long id, [FromBody] ConfirmStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.ConfirmAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Refuses an open branch stock request.
        /// </summary>
        /// <response code="200">The refused request — terminal.</response>
        /// <response code="403">A system-admin token attempted this.</response>
        /// <response code="404">No branch request exists with the supplied id.</response>
        /// <response code="409">The request is not InProgress or PartiallyConfirmed — once anything has been received it can't be walked back by refusing (decision D-06); settle the generated transfers instead.</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("{id:long}/refuse")]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Refuse(
            long id, [FromBody] RefuseStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.RefuseAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Cancels an open branch stock request.
        /// </summary>
        /// <response code="200">The cancelled request — terminal.</response>
        /// <response code="403">A system-admin token attempted this.</response>
        /// <response code="404">No branch request exists with the supplied id.</response>
        /// <response code="409">The request is not InProgress or PartiallyConfirmed — same rule as Refuse (decision D-06).</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPost("{id:long}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<StockRequestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Cancel(
            long id, [FromBody] CancelStockRequest request, CancellationToken cancellationToken)
            => (await _services.BranchRequests.CancelAsync(id, request, cancellationToken)).ToActionResult(this);
    }
}