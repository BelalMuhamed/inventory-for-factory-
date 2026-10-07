// InfrastructureLayer/Repositories/TenantRepo.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Tenants;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core implementation of <see cref="ITenantRepo"/>. All tenant query logic —
    /// filtering, the whitelisted sort mapping, paging, and soft-delete inclusion — lives here.</summary>
    public sealed class TenantRepo : GenericRepo<Tenant, long>, ITenantRepo
    {
        /// <summary>Creates the repository over the supplied context.</summary>
        /// <param name="context">The shared <see cref="AppDbContext"/>.</param>
        public TenantRepo(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<Tenant?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => await Set.FirstOrDefaultAsync(
                t => t.Username == username && t.IsActive,
                cancellationToken);

        /// <inheritdoc />
        public async Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(
            TenantListFilter filter, CancellationToken cancellationToken = default)
        {
            // Bypass the soft-delete filter so the tri-state IsDeleted can include deleted tenants.
            IQueryable<Tenant> query = Set.AsNoTracking().IgnoreQueryFilters();

            if (!string.IsNullOrWhiteSpace(filter.Username))
            {
                string term = filter.Username.Trim();
                query = query.Where(t => t.Username.Contains(term));
            }

            if (filter.IsActive is bool active)
            {
                query = query.Where(t => t.IsActive == active);
            }

            if (filter.IsDeleted is bool deleted)
            {
                query = query.Where(t => t.IsDeleted == deleted);
            }

            int totalCount = await query.CountAsync(cancellationToken);

            query = ApplySort(query, filter.SortBy, filter.SortDir);

            int pageNumber = Math.Max(filter.Page, 1);
            int skip = (pageNumber - 1) * filter.PageSize;

            List<Tenant> items = await query
                .Skip(skip)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        /// <inheritdoc />
        public async Task<Tenant?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default)
            => await Set.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        /// <inheritdoc />
        public async Task<bool> CodeExistsAsync(string code, long? excludeTenantId, CancellationToken cancellationToken = default)
            => await Set.IgnoreQueryFilters()
                .AnyAsync(t => t.Code == code && (excludeTenantId == null || t.Id != excludeTenantId), cancellationToken);

        /// <inheritdoc />
        public async Task<bool> UsernameExistsAsync(string username, long? excludeTenantId, CancellationToken cancellationToken = default)
            => await Set.IgnoreQueryFilters()
                .AnyAsync(t => t.Username == username && (excludeTenantId == null || t.Id != excludeTenantId), cancellationToken);

        // Maps a free-text sort field to a fixed column whitelist; unknown values fall back to
        // Username so an arbitrary client string can never reach the query as a raw expression.
        private static IQueryable<Tenant> ApplySort(IQueryable<Tenant> query, string? sortBy, string? sortDir)
        {
            bool descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            return sortBy?.Trim().ToLowerInvariant() switch
            {
                "code" => descending ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code),
                "isactive" => descending ? query.OrderByDescending(t => t.IsActive) : query.OrderBy(t => t.IsActive),
                "createdat" => descending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
                _ => descending ? query.OrderByDescending(t => t.Username) : query.OrderBy(t => t.Username)
            };
        }
    }
}