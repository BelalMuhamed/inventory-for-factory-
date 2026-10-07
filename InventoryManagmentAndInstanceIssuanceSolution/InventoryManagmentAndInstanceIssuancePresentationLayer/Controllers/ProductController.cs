using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Products;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Product (catalog) management endpoints (API Spec §4.6). Requires authentication: tenant callers
    /// manage their own products; a system admin manages any tenant's products. Hard delete is not
    /// exposed (consistent with the Branch/Tenant modules and the locked soft-delete-only decision).
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs. <c>Create</c> can additionally return this code with the
    /// standard envelope (<c>Auth.ActorNotResolved</c> — reused from the auth module, not a
    /// product-specific code) in the rare edge case where a token passes authentication but the
    /// acting tenant identity can't be resolved from it.
    /// </response>
    [ApiController]
    [Route("api/products")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class ProductController : ControllerBase
    {
        private readonly IServiceManager _services;

        public ProductController(IServiceManager services) => _services = services;

        /// <summary>Lists products with paging and filters.</summary>
        /// <response code="200">A page of products.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ProductResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] ProductListFilter filter, CancellationToken cancellationToken)
            => (await _services.Products.GetAllAsync(filter, cancellationToken)).ToActionResult(this);

        /// <summary>Gets a product by id.</summary>
        /// <response code="200">The product.</response>
        /// <response code="404">No product exists with the supplied id.</response>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
            => (await _services.Products.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Creates a product. Admin callers supply the target tenant id. A system admin may also
        /// attach a print configuration in the same call (Printing Module, phase 7); a tenant
        /// caller may still create a plain product without one.
        /// </summary>
        /// <response code="200">The created product.</response>
        /// <response code="403">A tenant caller supplied a Matica/Evolis print-configuration payload — attaching one at creation time is system-admin only.</response>
        /// <response code="409">A product with this name already exists for the tenant.</response>
        /// <response code="422">The request body failed validation, or (system-admin caller) the target tenant id is missing or doesn't exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
            => (await _services.Products.CreateAsync(request, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Updates a product's name, status, threshold, and transaction-way. Printer family is
        /// not changeable here — use <c>PUT /api/products/{id}/print-config</c>.
        /// </summary>
        /// <response code="200">The updated product.</response>
        /// <response code="404">No product exists with the supplied id.</response>
        /// <response code="409">The new name is already taken, or ProductTransactionWay was changed on a product that already has cards in inventory (immutable once cards exist).</response>
        /// <response code="422">The request body failed validation.</response>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
            => (await _services.Products.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Soft-deletes a product.</summary>
        /// <response code="200">The product was soft-deleted; the payload is null.</response>
        /// <response code="404">No product exists with the supplied id.</response>
        /// <response code="409">The product is already deleted, or is part of an open branch stock request line.</response>
        [HttpDelete("{id:long}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
            => (await _services.Products.SoftDeleteAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Restores a soft-deleted product.</summary>
        /// <response code="200">The product was restored; the payload is null.</response>
        /// <response code="404">No product exists with the supplied id.</response>
        /// <response code="409">The product is not currently deleted.</response>
        [HttpPost("{id:long}/restore")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Restore(long id, CancellationToken cancellationToken)
            => (await _services.Products.RestoreAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Activates a product (idempotent).</summary>
        /// <response code="200">The product is now active.</response>
        /// <response code="404">No product exists with the supplied id.</response>
        [HttpPost("{id:long}/activate")]
        [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
            => (await _services.Products.ActivateAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Deactivates a product (idempotent).</summary>
        /// <response code="200">The product is now inactive.</response>
        /// <response code="404">No product exists with the supplied id.</response>
        [HttpPost("{id:long}/deactivate")]
        [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
            => (await _services.Products.DeactivateAsync(id, cancellationToken)).ToActionResult(this);
    }
}
