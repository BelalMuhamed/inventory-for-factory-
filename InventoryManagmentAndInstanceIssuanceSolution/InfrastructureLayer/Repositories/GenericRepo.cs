using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IGenericRepo{T, TKey}"/>. Named repositories derive
    /// from this type and add entity-specific query methods. Changes are staged against the
    /// shared change tracker and committed by the unit of work.
    /// </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <typeparam name="TKey">Primary-key type.</typeparam>
    public class GenericRepo<T, TKey> : IGenericRepo<T, TKey>
        where T : class
    {
        /// <summary>The shared context; exposed to derived repositories for query composition.</summary>
        protected readonly AppDbContext Context;

        /// <summary>The entity set for <typeparamref name="T"/>.</summary>
        protected readonly DbSet<T> Set;

        /// <summary>Creates the repository over the supplied context.</summary>
        /// <param name="context">The shared <see cref="AppDbContext"/>.</param>
        public GenericRepo(AppDbContext context)
        {
            Context = context;
            Set = context.Set<T>();
        }

        /// <inheritdoc />
        public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
            => await Set.FindAsync(new object?[] { id }, cancellationToken);

        /// <inheritdoc />
        public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().ToListAsync(cancellationToken);

        /// <inheritdoc />
        public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await Set.AddAsync(entity, cancellationToken);

        /// <inheritdoc />
        public virtual void Update(T entity) => Set.Update(entity);

        /// <inheritdoc />
        public virtual void Remove(T entity) => Set.Remove(entity);
    }
}
