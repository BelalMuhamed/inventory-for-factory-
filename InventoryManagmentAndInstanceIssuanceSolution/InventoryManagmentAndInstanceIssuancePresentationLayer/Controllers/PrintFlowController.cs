using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.ProductItems;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Controllers
{
    /// <summary>
    /// The two Matica Print Flow backend calls, exposed only to the Printer Agent — never to
    /// Angular directly, never under the caller's own tenant/admin session token. Deliberately a
    /// separate controller from <see cref="ProductItemsController"/>, following the same reasoning
    /// documented on <see cref="CardFilesController"/>: two authorization postures (a normal
    /// tenant/admin session vs. a Print Agent token, signed with an entirely different key) in one
    /// controller would be legal and confusing.
    /// <para>
    /// <c>[Authorize]</c> is applied per action, not at the class level (Matica Print Flow,
    /// reconciliation-credential phase) — <see cref="ResolveForPrint"/> only ever runs during a
    /// live print attempt, so it stays <see cref="AuthorizationPolicies.PrintAgentOnly"/>-only;
    /// <see cref="RecordPrintResult"/> is also called by the background reconciliation job, so it
    /// uses <see cref="AuthorizationPolicies.PrintResultAuthorized"/> instead, which accepts either
    /// token type. Multiple <c>[Authorize]</c> attributes on a class and its methods combine with
    /// AND semantics in ASP.NET Core, not "most specific wins" — a class-level
    /// <c>PrintAgentOnly</c> attribute would have stacked with an action-level policy rather than
    /// been replaced by it, which is why this controller has no class-level <c>[Authorize]</c> at
    /// all and both actions declare their own.
    /// </para>
    /// <para>
    /// Both actions additionally cross-check the request body's <c>BranchId</c> against the
    /// authenticated token's own <c>branchId</c> claim — every token type here carries one, so a
    /// leaked or reused token of either kind cannot be pointed at a different branch just by
    /// changing the request body.
    /// </para>
    /// </summary>
    /// <response code="401">No valid Print Agent or reconciliation service token was supplied.</response>
    [ApiController]
    [Route("api/print-flow")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public sealed class PrintFlowController : ControllerBase
    {
        private readonly IServiceManager _services;

        /// <summary>Creates the controller from the service façade.</summary>
        public PrintFlowController(IServiceManager services) => _services = services;

        /// <summary>
        /// Backend Call #1 (Matica Print Flow): resolves and validates exactly one physical card
        /// for printing, called by the Printer Agent right after <c>ReadMAG</c>. The raw PAN
        /// travels only in this request's body, over TLS, and is discarded server-side once
        /// fingerprinted — see <see cref="ApplicationLayer.ServicesContracts.IProductItemService.ResolveForPrintAsync"/>.
        /// </summary>
        /// <param name="request">The raw PAN plus the product/branch this card is expected to match.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The card is printable; returns its id for Backend Call #2.</response>
        /// <response code="403">The request's <c>BranchId</c> does not match the token's own scope.</response>
        /// <response code="404">No printable card matches the supplied PAN/product/branch.</response>
        /// <response code="409">The branch has insufficient Unknown-way stock for this product.</response>
        /// <response code="422">The supplied PAN is not well-formed.</response>
        [HttpPost("resolve-for-print")]
        [Authorize(Policy = AuthorizationPolicies.PrintAgentOnly)]
        [ProducesResponseType(typeof(ApiResponse<ResolveForPrintResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ResolveForPrint(
            [FromBody] ResolveForPrintRequest request, CancellationToken cancellationToken)
        {
            if (IsOutsideTokenScope(request.BranchId))
            {
                return Result.Failure<ResolveForPrintResponse>(ProductItemErrors.PrintFlowScopeMismatch())
                    .ToActionResult(this);
            }

            return (await _services.ProductItems.ResolveForPrintAsync(request, cancellationToken)).ToActionResult(this);
        }

        /// <summary>
        /// Backend Call #2 (Matica Print Flow): records the physical outcome of one print attempt,
        /// called by the Printer Agent right after <c>EjectCard</c>. Safely retryable — see
        /// <see cref="ApplicationLayer.ServicesContracts.IProductItemService.RecordPrintResultAsync"/>
        /// for the lightweight idempotency behavior.
        /// </summary>
        /// <param name="productItemId">The card id returned by <see cref="ResolveForPrint"/>.</param>
        /// <param name="request">The physical outcome, branch, cardholder name, and idempotency key.</param>
        /// <param name="cancellationToken">Request cancellation token.</param>
        /// <response code="200">The result was recorded (or the retry was recognized as already applied).</response>
        /// <response code="403">The request's <c>BranchId</c> does not match the token's own scope.</response>
        /// <response code="404">No such card, or it no longer matches the expected branch/status.</response>
        /// <response code="409">The card is already disposed, or the branch has insufficient Unknown-way stock.</response>
        [HttpPost("{productItemId:long}/print-result")]
        [Authorize(Policy = AuthorizationPolicies.PrintResultAuthorized)]
        [ProducesResponseType(typeof(ApiResponse<ProductItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RecordPrintResult(
            long productItemId, [FromBody] RecordPrintResultRequest request, CancellationToken cancellationToken)
        {
            if (IsOutsideTokenScope(request.BranchId))
            {
                return Result.Failure<ProductItemResponse>(ProductItemErrors.PrintFlowScopeMismatch())
                    .ToActionResult(this);
            }

            return (await _services.ProductItems.RecordPrintResultAsync(productItemId, request, cancellationToken))
                .ToActionResult(this);
        }

        /// <summary>
        /// Defense in depth: whichever token authenticated the caller — a live Print Agent token
        /// or a background reconciliation service token — already scopes its holder to one branch
        /// via a <c>branchId</c> claim, set once at mint time (by <c>AuthController.CreatePrintAgentToken</c>
        /// or <c>AuthController.CreateServiceToken</c> respectively, both after validating tenant
        /// ownership). Both token types use the same claim type name for this, so one check here
        /// covers either; this just confirms the request body cannot silently disagree with
        /// whichever one is actually present — a leaked token of either kind can't be redirected
        /// to a different branch by editing the payload alone.
        /// </summary>
        private bool IsOutsideTokenScope(long requestedBranchId) =>
            User.FindFirstValue(PrintAgentTokenGenerator.BranchIdClaim) != requestedBranchId.ToString();
    }
}
