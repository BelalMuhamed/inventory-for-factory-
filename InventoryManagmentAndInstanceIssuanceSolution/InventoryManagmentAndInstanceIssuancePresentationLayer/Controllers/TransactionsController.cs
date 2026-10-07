using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Transfers;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Card-transfer endpoints (API §4.10). Requires authentication. A tenant caller creates and
    /// settles its own tenant's transfers; a system admin has read-only access across tenants
    /// (decision Q7) — create, receive, and dispose all reject an admin token outright.
    /// <para>
    /// Every line — Known-way or Unknown-way — now follows the same create-then-settle shape
    /// (Unknown-way Maker-Checker workflow): creating a transfer never finalizes stock movement by
    /// itself, and every transfer opens <c>InProgress</c> until a separate <see cref="Receive"/>
    /// call states what was actually confirmed. The same account may create and later settle a
    /// transfer — both identities are recorded on the transfer regardless (see
    /// <c>TransferDetailResponse.CreatedByUsername</c>/<c>CheckedByUsername</c>).
    /// </para>
    /// <para>
    /// There is no separate refuse endpoint. Every settlement outcome — received, disposed, or
    /// (for whatever is left over) returned — goes through <see cref="Receive"/>, which carries a
    /// disposition per product line: an explicit per-card outcome for a Known-way line, or a
    /// received quantity plus (when there's a remainder) a difference action for an Unknown-way
    /// line. An Unknown-way line cannot be disposed of — it moves entitlement only, so there is no
    /// physical card to write off.
    /// </para>
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs — no action here has a service-level 401 path
    /// (<c>Transfer.ActorNotResolved</c> exists in the catalogue but is, per its own doc comment,
    /// unreachable behind <c>[Authorize]</c> with a valid tenant token).
    /// </response>
    [ApiController]
    [Route("api/inventory/transactions")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class TransactionsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public TransactionsController(IServiceManager services) => _services = services;

        /// <summary>Lists transfers with paging and filters.</summary>
        /// <response code="200">A page of transfers, scoped to the caller's tenant (or any tenant, for a system admin).</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<TransferListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] TransferListFilter filter, CancellationToken cancellationToken)
            => (await _services.Transfers.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets one transfer's full detail, including product lines and card-level items.</summary>
        /// <response code="200">The transfer. Items is empty for a transfer carrying only Unknown-way lines.</response>
        /// <response code="404">No transfer exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<TransferDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Transfers.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Creates a direct transfer between two branches of the caller's tenant. Known-way
        /// product lines must name the exact cards being sent; Unknown-way lines must not.
        /// </summary>
        /// <response code="200">The created transfer — opens InProgress; no stock has moved yet.</response>
        /// <response code="403">A system-admin token attempted this — admin access to this module is read-only (decision Q7).</response>
        /// <response code="404">The source/target branch or a named product doesn't exist.</response>
        /// <response code="409">A named card isn't currently available to move (already in flight, printed, expired, spoiled, or disposed).</response>
        /// <response code="422">The request body failed validation — see TransferErrors.cs for the full catalogue (same/different branch, no lines, duplicate product, wrong card-id shape for the product's transaction way, etc.).</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<TransferDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreateTransferRequest request, CancellationToken cancellationToken)
            => (await _services.Transfers.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Settles an in-progress transfer: a per-product disposition of received and disposed
        /// quantities. For a Known-way line, whatever is left over always spawns a new transfer
        /// back to the source. For an Unknown-way line with a remainder, the caller states a
        /// difference action: <c>ReturnedToSource</c> spawns a return transfer exactly like the
        /// Known-way case, while <c>KeptAtDestination</c> credits the full quantity to the target
        /// immediately and spawns nothing.
        /// </summary>
        /// <response code="200">Settlement outcome — returnTransferId/disposalId are set only when a remainder actually moved/was written off.</response>
        /// <response code="403">A system-admin token attempted this.</response>
        /// <response code="404">No transfer exists with the supplied id.</response>
        /// <response code="409">The transfer was already settled — settlement cannot run twice.</response>
        /// <response code="422">The request body failed validation — a product was omitted, a Known-way line is missing per-card dispositions, an Unknown-way line's remainder has no difference action, etc. See TransferErrors.cs for the full catalogue.</response>
        [HttpPost("{id:long}/receive")]
        [ProducesResponseType(typeof(ApiResponse<SettleTransferResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Receive(
            long id, [FromBody] ReceiveTransferRequest request, CancellationToken cancellationToken)
            => (await _services.Transfers.ReceiveAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Writes off everything an in-progress transfer still carries, in one step, without
        /// receiving any of it. Equivalent to <see cref="Receive"/> with every line fully disposed.
        /// </summary>
        /// <response code="200">Equivalent to Receive with every line fully disposed.</response>
        /// <response code="403">A system-admin token attempted this.</response>
        /// <response code="404">No transfer exists with the supplied id.</response>
        /// <response code="409">The transfer was already settled.</response>
        /// <response code="422">The disposing branch was omitted, or the request body otherwise failed validation.</response>
        [HttpPost("{id:long}/dispose")]
        [ProducesResponseType(typeof(ApiResponse<SettleTransferResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Dispose(
            long id, [FromBody] DisposeTransferRequest request, CancellationToken cancellationToken)
            => (await _services.Transfers.DisposeAsync(id, request, cancellationToken)).ToActionResult(this);
    }
}
