using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.CardFiles;
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
    /// Card-file generation endpoint (Card File Generation, Phase 9.6). Restricted to the
    /// bootstrap system admin via <see cref="AuthorizationPolicies.SystemAdminOnly"/>, following
    /// <see cref="TenantsController"/>'s pattern.
    /// <para>
    /// Deliberately a separate controller from <see cref="InventoryController"/>: that one is
    /// class-level <c>[Authorize]</c> and acts for the calling tenant, this one is admin-only and
    /// acts <em>on behalf of</em> a tenant. Two authorization postures in one controller would be
    /// legal and confusing.
    /// </para>
    /// </summary>
    /// <response code="401">
    /// No valid bearer token was supplied. Typically the authorization middleware's empty-body
    /// rejection before this action runs — same as <c>TenantsController</c> (S1). <see cref="Generate"/>
    /// can additionally return this code with the standard envelope (<c>CardFile.ActorNotResolved</c>)
    /// in the rare edge case where a token passes the system-admin policy check but the acting
    /// principal still can't be resolved from it — a defensive re-check, not an expected scenario.
    /// </response>
    /// <response code="403">
    /// The token is valid but is not a system-admin token. Always the authorization middleware's
    /// empty-body rejection.
    /// </response>
    [ApiController]
    [Route("api/card-files")]
    [Authorize(Policy = AuthorizationPolicies.SystemAdminOnly)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class CardFilesController : ControllerBase
    {
        /// <summary>Binary content type used for the generated <c>.dat</c> file download.</summary>
        private const string DatFileContentType = "application/octet-stream";

        /// <summary>Response header carrying the file's SHA-256 fingerprint (uppercase hex).</summary>
        private const string FileMacHeader = "X-File-Mac";

        /// <summary>Response header carrying the number of cards written to the file.</summary>
        private const string CardCountHeader = "X-Card-Count";

        /// <summary>Response header carrying the row count the tenant must declare on upload.</summary>
        private const string ExpectedRowCountHeader = "X-Expected-Row-Count";

        private readonly IServiceManager _services;

        /// <summary>Creates the controller from the service façade.</summary>
        public CardFilesController(IServiceManager services)
        {
            _services = services;
        }

        /// <summary>
        /// Generates an encrypted <c>.dat</c> card file for a tenant and streams it back as a raw
        /// binary download. Because the response body <em>is</em> the file, the hand-off metadata
        /// the tenant needs (<c>fileMac</c>, <c>cardCount</c>, <c>expectedRowCount</c>) travels in
        /// the <see cref="FileMacHeader"/>, <see cref="CardCountHeader"/>, and
        /// <see cref="ExpectedRowCountHeader"/> response headers instead of a JSON body.
        /// </summary>
        /// <remarks>
        /// The request body contains full PANs in the clear — the only endpoint in the platform
        /// that accepts them. Callers must use TLS, and the file must be delivered to the tenant
        /// over a channel at least as protected as the one it arrived on.
        /// <para>
        /// All-or-nothing: if any card fails validation the response is 422 and no file is
        /// produced. The per-card reasons are in <c>error.validationErrors</c>, keyed by the
        /// card's index in the request.
        /// </para>
        /// </remarks>
        /// <param name="request">Target tenant and the cards to include.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">
        /// The generated <c>.dat</c> file as <c>application/octet-stream</c>, with
        /// <c>Content-Disposition: attachment</c> naming it. <c>X-File-Mac</c>,
        /// <c>X-Card-Count</c>, and <c>X-Expected-Row-Count</c> headers carry the hand-off
        /// metadata previously returned in the JSON body.
        /// </response>
        /// <response code="401">
        /// Beyond the usual empty-body middleware rejection (see the controller-level 401 doc),
        /// this action can also return 401 <em>with</em> the standard envelope
        /// (<c>CardFile.ActorNotResolved</c>) in the rare edge case described there.
        /// </response>
        /// <response code="404">No tenant exists with the supplied id.</response>
        /// <response code="409">The tenant is inactive or deleted.</response>
        /// <response code="422">The card list is empty, exceeds the cap, or contains rejected cards.</response>
        [HttpPost]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Generate(
            [FromBody] CardFileGenerationRequest request,
            CancellationToken cancellationToken)
        {
            // The payload is cardholder data. Keep it out of every intermediary cache.
            Response.Headers.CacheControl = "no-store";

            Result<CardFileGenerationResult> result =
                await _services.CardFiles.GenerateAsync(request, cancellationToken);

            if (result.IsFailure)
            {
                // Same JSON ApiResponse envelope and status-code mapping as every other endpoint;
                // only the success path returns a raw file.
                return result.ToActionResult(this);
            }

            CardFileGenerationResult file = result.Value;

            Response.Headers[FileMacHeader] = file.FileMac;
            Response.Headers[CardCountHeader] = file.CardCount.ToString(CultureInfo.InvariantCulture);
            Response.Headers[ExpectedRowCountHeader] =
                file.ExpectedRowCount.ToString(CultureInfo.InvariantCulture);

            return File(file.FileContent, DatFileContentType, file.FileName);
        }
    }
}
