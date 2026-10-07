using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Generic data-access contract for a single aggregate type. Entity-specific query logic is
    /// expressed as named methods on derived repository interfaces (e.g. <c>ITenantRepo</c>);
    /// raw query predicates never appear in the service layer.
    /// <para>
    /// Implementations stage changes against the change tracker; persistence is committed by
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> so several repository operations can share a
    /// single transaction.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Aggregate/entity type managed by the repository.</typeparam>
    /// <typeparam name="TKey">Type of the entity's primary key.</typeparam>
    public interface IGenericRepo<T, TKey>
        where T : class
    {
        /// <summary>Finds an entity by its primary key.</summary>
        /// <param name="id">Primary key value.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        /// <returns>The entity, or <c>null</c> when no row matches (or it is filtered out).</returns>
        Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>Returns all entities visible under the active query filters.</summary>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Stages a new entity for insertion.</summary>
        /// <param name="entity">The entity to add.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>Stages an existing entity as modified.</summary>
        /// <param name="entity">The entity to update.</param>
        void Update(T entity);

        /// <summary>Stages an entity for removal (hard delete; soft delete is handled by the service layer).</summary>
        /// <param name="entity">The entity to remove.</param>
        void Remove(T entity);
    }
}
