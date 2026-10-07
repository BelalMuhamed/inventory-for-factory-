using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.BranchRequests;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Branch stock request use cases (API §4.9): raise a request, confirm it against one or more
    /// generated transfers (repeatable — decision Q-04), or close it early by refusal or
    /// cancellation.
    /// <para>
    /// Under the shipped single-account-per-tenant auth model there is no separate requester and
    /// confirmer — every write is performed by the same tenant account (§4, §11). Reads are
    /// available to a system admin across tenants; every write requires an authenticated tenant
    /// and rejects a system-admin caller outright, matching the precedent
    /// <c>ITransferService</c> already set (decision Q7 of the Transfers workstream) — a
    /// request's <c>ActionTakenByTenantId</c> has nowhere to point for an admin token.
    /// </para>
    /// </summary>
    public interface IBranchRequestService
    {
        /// <summary>Lists requests in the caller's scope (API §4.9 list endpoint).</summary>
        Task<Result<PaginatedResponse<StockRequestListItemResponse>>> GetAllAsync(
            StockRequestListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one request's full detail, including lines, fulfilment counters, unrequested
        /// products (decision D-05), and the ids of every transfer it has generated.
        /// </summary>
        Task<Result<StockRequestDetailResponse>> GetByIdAsync(
            long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a stock request: validates the requesting branch (tenant-owned, not deleted,
        /// active — decision Q-13) and every product line, and blocks a request that would
        /// overlap an existing open one for the same branch (decision Q-11 / D-08). Reserves no
        /// stock and moves nothing.
        /// </summary>
        Task<Result<StockRequestDetailResponse>> CreateAsync(
            CreateStockRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Confirms a request against one or more transfer plans: validates every plan, then
        /// stages every transfer and credits the request's fulfilment counters inside one
        /// transaction — either all of it commits, or none of it does. Over-fulfilment and
        /// products outside the request are both allowed (decision Q-03); an Unknown-way line
        /// settles, and credits <c>ReceivedQuantity</c>, in this same call (Unknown Inventory
        /// Refactor). Callable repeatedly from any non-terminal status.
        /// </summary>
        Task<Result<ConfirmStockRequestResult>> ConfirmAsync(
            long id, ConfirmStockRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refuses a request without generating anything. Reason is optional (decision Q-10).
        /// Allowed from <c>InProgress</c> or <c>PartiallyConfirmed</c> only (decision D-06) —
        /// already-dispatched transfers are left to complete their own §4.10 lifecycle.
        /// </summary>
        Task<Result<StockRequestDetailResponse>> RefuseAsync(
            long id, RefuseStockRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a request without generating anything. Same status guard as
        /// <see cref="RefuseAsync"/> (decision D-06).
        /// </summary>
        Task<Result<StockRequestDetailResponse>> CancelAsync(
            long id, CancelStockRequest request, CancellationToken cancellationToken = default);
    }
}
