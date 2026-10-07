using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.ProductItems;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Product-item endpoints (API Spec §4.7). Requires authentication; tenant callers are scoped to
    /// their own tenant. The update recomputes branch stock in the same transaction.
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs — no action here has a service-level 401 path.
    /// </response>
    [ApiController]
    [Route("api/product-items")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class ProductItemsController : ControllerBase
    {
        private readonly IServiceManager _services;

        public ProductItemsController(IServiceManager services) => _services = services;

        /// <summary>Lists product items with paging and filters (code, status, productId, productName, branchId).</summary>
        /// <response code="200">A page of product items.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ProductItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] ProductItemListFilter filter, CancellationToken cancellationToken)
            => (await _services.ProductItems.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets a product item by id.</summary>
        /// <response code="200">The product item.</response>
        /// <response code="404">No product item exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<ProductItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.ProductItems.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Updates status, holder name and notes; recomputes branch stock transactionally.</summary>
        /// <response code="200">The updated item.</response>
        /// <response code="404">No product item exists with the supplied id.</response>
        /// <response code="409">The card is in transit/unassigned (branchId is null) and can't be modified here, or is already disposed (terminal).</response>
        /// <response code="422">The request body failed validation, or attempted to set status to Disposed through this endpoint — use the dedicated dispose endpoint instead.</response>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<ProductItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductItemRequest request, CancellationToken cancellationToken)
            => (await _services.ProductItems.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);
    }
}