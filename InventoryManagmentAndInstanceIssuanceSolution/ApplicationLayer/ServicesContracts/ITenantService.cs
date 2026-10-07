// ApplicationLayer/ServicesContracts/ITenantService.cs
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Tenants;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Tenant management use cases (API Spec §4.2). Access is restricted to the system admin at
    /// the presentation layer. Every operation returns a <see cref="Result"/> outcome; business
    /// failures (missing tenant, duplicate code/username, invalid delete/restore state) surface as
    /// categorized <see cref="Error"/>s rather than exceptions. Hard delete is intentionally omitted.
    /// </summary>
    public interface ITenantService
    {
        /// <summary>
        /// Returns a page of tenants matching the supplied filter. The tri-state
        /// <see cref="TenantListFilter.IsDeleted"/> controls inclusion of soft-deleted tenants.
        /// </summary>
        /// <param name="filter">Filter, paging, and sort inputs.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<PaginatedResponse<TenantResponse>>> GetAllAsync(
            TenantListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a single tenant by id, including soft-deleted tenants.
        /// </summary>
        /// <param name="id">Tenant primary key.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The tenant, or an <see cref="ErrorCategory.NotFound"/> error.</returns>
        Task<Result<TenantResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a tenant, hashing the supplied password. Fails with
        /// <see cref="ErrorCategory.Conflict"/> when the code or username is already in use
        /// (including by a soft-deleted tenant).
        /// </summary>
        /// <param name="request">The creation request.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The created tenant on success.</returns>
        Task<Result<TenantResponse>> CreateAsync(
            CreateTenantRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a tenant's username, code, and active state. Fails with
        /// <see cref="ErrorCategory.NotFound"/> when the tenant does not exist, or
        /// <see cref="ErrorCategory.Conflict"/> on a duplicate code/username.
        /// </summary>
        /// <param name="id">Tenant primary key.</param>
        /// <param name="request">The update request.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result<TenantResponse>> UpdateAsync(
            long id, UpdateTenantRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a tenant's password with a freshly hashed value.
        /// </summary>
        /// <param name="id">Tenant primary key.</param>
        /// <param name="request">The new password.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> ChangePasswordAsync(
            long id, ChangeTenantPasswordRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes a tenant. The acting principal is identified by <paramref name="actorUsername"/>
        /// (from the token); the service resolves their id and records it in <c>DeletedBy</c>.
        /// </summary>
        /// <param name="id">Tenant primary key.</param>
        /// <param name="actorUsername">Authenticated username of the system admin performing the delete.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> SoftDeleteAsync(long id, string actorUsername, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a soft-deleted tenant, clearing its soft-delete fields. Fails with
        /// <see cref="ErrorCategory.NotFound"/> when the tenant does not exist, or
        /// <see cref="ErrorCategory.Conflict"/> when it is not currently deleted.
        /// </summary>
        /// <param name="id">Tenant primary key.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default);
    }
}