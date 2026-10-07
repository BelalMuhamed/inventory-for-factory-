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
    /// Aggregated transaction history view (API §4.10). A reporting alias over the same data as
    /// <see cref="TransactionsController.GetAll"/> — same scope, same filter, same service call —
    /// kept as its own thin controller rather than a second route on
    /// <see cref="TransactionsController"/> so the two stay easy to tell apart in routing tables
    /// and Swagger without either name implying it owns the other.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs.
    /// </response>
    [ApiController]
    [Route("api/inventory/history")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class InventoryHistoryController : ControllerBase
    {
        private readonly IServiceManager _services;

        public InventoryHistoryController(IServiceManager services) => _services = services;

        /// <summary>Lists transfer history with paging and filters — identical scope to §4.10's transaction list.</summary>
        /// <response code="200">A page of transfer history, newest first by default.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<TransferListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] TransferListFilter filter, CancellationToken cancellationToken)
            => (await _services.Transfers.GetAllAsync(filter, cancellationToken)).ToActionResult(this);
    }
}
