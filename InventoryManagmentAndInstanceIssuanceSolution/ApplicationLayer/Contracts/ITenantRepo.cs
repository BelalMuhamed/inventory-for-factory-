// ApplicationLayer/Contracts/ITenantRepo.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Tenants;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Repository for <see cref="Tenant"/> aggregates. All tenant query logic — filtering, the
    /// whitelisted sort mapping, paging, and soft-delete inclusion — is encapsulated behind named
    /// methods here so the service layer never composes raw predicates.
    /// </summary>
    public interface ITenantRepo : IGenericRepo<Tenant, long>
    {
        /// <summary>
        /// Finds an active, non-deleted tenant by its login username. Username comparison is
        /// case-insensitive at the database collation level.
        /// </summary>
        /// <param name="username">The login username supplied at authentication.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The matching tenant, or <c>null</c> when none is found or it is inactive.</returns>
        Task<Tenant?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a single page of tenants matching <paramref name="filter"/>, together with the
        /// total count of matches before paging. Bypasses the soft-delete query filter so the
        /// tri-state <see cref="TenantListFilter.IsDeleted"/> can include deleted tenants (REQ §4.2).
        /// The sort field is resolved against a fixed column whitelist; unknown values fall back to
        /// a safe default.
        /// </summary>
        /// <param name="filter">Filter, paging, and sort inputs.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The page items and the total match count.</returns>
        Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(
            TenantListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a tenant by id including soft-deleted rows, so details and restore can operate on
        /// deleted tenants (REQ §4.2). Returns <c>null</c> when no row has the id.
        /// </summary>
        /// <param name="id">Tenant primary key.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Tenant?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether any tenant — including soft-deleted ones — already uses
        /// <paramref name="code"/>, optionally excluding the tenant identified by
        /// <paramref name="excludeTenantId"/> (used on update).
        /// </summary>
        /// <param name="code">Candidate slug.</param>
        /// <param name="excludeTenantId">Tenant id to exclude from the check, or <c>null</c> on create.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<bool> CodeExistsAsync(string code, long? excludeTenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether any tenant — including soft-deleted ones — already uses
        /// <paramref name="username"/>, optionally excluding the tenant identified by
        /// <paramref name="excludeTenantId"/> (used on update).
        /// </summary>
        /// <param name="username">Candidate login username.</param>
        /// <param name="excludeTenantId">Tenant id to exclude from the check, or <c>null</c> on create.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<bool> UsernameExistsAsync(string username, long? excludeTenantId, CancellationToken cancellationToken = default);
    }
}