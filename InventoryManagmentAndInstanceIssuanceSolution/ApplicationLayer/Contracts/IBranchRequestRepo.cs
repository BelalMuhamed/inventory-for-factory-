using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.BranchRequests;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="BranchRequest"/> and its lines (ERD §4.1–§4.2,
    /// API §4.9). All query logic is expressed here as named methods; raw predicates never reach
    /// the service layer.
    /// </summary>
    public interface IBranchRequestRepo : IGenericRepo<BranchRequest, long>
    {
        /// <summary>
        /// Returns a page of requests with the requesting branch and lines eager-loaded, scoped
        /// as for stock and product items. A tenant caller passes its
        /// <paramref name="tenantScopeId"/>; a system admin passes <c>null</c> and may filter via
        /// <see cref="StockRequestListFilter.TenantId"/> (§11: admin access is read-only).
        /// </summary>
        Task<(IReadOnlyList<BranchRequest> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, StockRequestListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one request by id (no tracking) with the requesting branch, its lines, and each
        /// line's product eager-loaded — everything <see cref="StockRequestDetailResponse"/>
        /// needs in one query.
        /// </summary>
        /// <param name="id">Request id.</param>
        /// <param name="tenantScopeId">Tenant scope, or <c>null</c> for a system admin.</param>
        /// <returns>The request, or <c>null</c> when it does not exist or is outside scope.</returns>
        Task<BranchRequest?> GetDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads one tracked request by id, with its lines included, for in-transaction mutation
        /// (confirm, refuse, cancel). <see cref="BranchRequest.RowVersion"/> comes along for the
        /// optimistic-concurrency check the caller performs before mutating (decision Q-07).
        /// </summary>
        /// <param name="id">Request id.</param>
        /// <param name="tenantScopeId">Tenant scope, or <c>null</c> for a system admin.</param>
        /// <returns>The request, or <c>null</c> when it does not exist or is outside scope.</returns>
        Task<BranchRequest?> GetForUpdateAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new request — header and lines, reachable through the aggregate's navigation
        /// collection — for insertion in a single call.
        /// </summary>
        Task AddAsync(BranchRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Product ids appearing on any line of a non-terminal request (any status other than
        /// <see cref="DomainLayer.Enums.BranchRequestStatus.Fulfilled"/>,
        /// <see cref="DomainLayer.Enums.BranchRequestStatus.Refused"/>, or
        /// <see cref="DomainLayer.Enums.BranchRequestStatus.Cancelled"/>) raised by the given
        /// branch. Backs the duplicate-open-request guard at creation (decision Q-11 / D-08).
        /// </summary>
        Task<IReadOnlyCollection<long>> GetOpenProductIdsForBranchAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when the branch is the requesting branch of any non-terminal request (same
        /// definition as <see cref="GetOpenProductIdsForBranchAsync"/>). Backs
        /// <c>BranchService.SoftDeleteAsync</c>'s new guard (EC-R36): a branch cannot be deleted
        /// while it still has stock demand outstanding.
        /// </summary>
        Task<bool> HasOpenRequestForBranchAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when the product appears on any line of any non-terminal request (same
        /// definition as <see cref="GetOpenProductIdsForBranchAsync"/>). Backs
        /// <c>ProductService.SoftDeleteAsync</c>'s new guard (EC-R37).
        /// </summary>
        Task<bool> HasOpenRequestForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);
    }
}
