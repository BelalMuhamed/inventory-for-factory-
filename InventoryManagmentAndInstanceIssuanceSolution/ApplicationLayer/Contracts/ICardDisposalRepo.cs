using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Disposals;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="CardDisposal"/> (API §4.10, Addendum A).
    /// </summary>
    public interface ICardDisposalRepo : IGenericRepo<CardDisposal, long>
    {
        /// <summary>
        /// Returns a page of disposals with the disposing branch eager-loaded, scoped as for
        /// transfers. A tenant caller passes its <paramref name="tenantScopeId"/>; a system admin
        /// passes <c>null</c> and may filter via <see cref="DisposalListFilter.TenantId"/>.
        /// </summary>
        Task<(IReadOnlyList<CardDisposal> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, DisposalListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one disposal by id (no tracking) with the branch and every written-off card
        /// (with its product) eager-loaded.
        /// </summary>
        /// <param name="id">Disposal id.</param>
        /// <param name="tenantScopeId">Tenant scope, or <c>null</c> for a system admin.</param>
        /// <returns>The disposal, or <c>null</c> when it does not exist or is outside scope.</returns>
        Task<CardDisposal?> GetDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new disposal — header and item rows, reachable through the aggregate's
        /// navigation collection — for insertion in a single call.
        /// </summary>
        Task AddAsync(CardDisposal disposal, CancellationToken cancellationToken = default);
    }
}
