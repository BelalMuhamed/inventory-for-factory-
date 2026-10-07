using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.ServicesContracts;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// Ribbon type reference-table endpoint (Printing Module Q-05/Q-19). Read-only, open to both
    /// tenant and system-admin callers — the same global, non-tenant-scoped lookup list an Evolis
    /// printer's <c>DefaultRibbonTypeId</c> and a product's <c>EvolisProductPrintConfiguration</c>
    /// both reference.
    /// </summary>
    /// <response code="401">No valid bearer token was supplied.</response>
    [ApiController]
    [Route("api/ribbon-types")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class RibbonTypesController : ControllerBase
    {
        private readonly IServiceManager _services;

        public RibbonTypesController(IServiceManager services) => _services = services;

        /// <summary>Lists every ribbon type.</summary>
        /// <response code="200">Every ribbon type.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RibbonTypeResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
            => (await _services.RibbonTypes.GetAllAsync(cancellationToken)).ToActionResult(this);
    }
}
