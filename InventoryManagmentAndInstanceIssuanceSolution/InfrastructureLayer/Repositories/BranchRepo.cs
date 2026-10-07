using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Branches;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="Branch"/>.</summary>
    public sealed class BranchRepo : GenericRepo<Branch, long>, IBranchRepo
    {
        private readonly AppDbContext _context;

        public BranchRepo(AppDbContext context) : base(context) => _context = context;

        public async Task<(IReadOnlyList<Branch> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, BranchListFilter filter, CancellationToken cancellationToken = default)
        {
            // Ignore the global soft-delete filter so the tri-state IsDeleted can be applied explicitly.
            IQueryable<Branch> query = _context.Set<Branch>().IgnoreQueryFilters().AsNoTracking();

            if (tenantScopeId is long scope)
            {
                query = query.Where(b => b.TenantId == scope);          // tenant caller: forced scope
            }
            else if (filter.TenantId is long requested)
            {
                query = query.Where(b => b.TenantId == requested);      // admin caller: optional filter
            }

            if (filter.IsDeleted is bool deleted)
            {
                query = query.Where(b => b.IsDeleted == deleted);
            }

            if (filter.IsActive is bool active)
            {
                query = query.Where(b => b.IsActive == active);
            }

            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                query = query.Where(b => b.Name.Contains(filter.Name));
            }

            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "name" => desc ? query.OrderByDescending(b => b.Name) : query.OrderBy(b => b.Name),
                "isactive" => desc ? query.OrderByDescending(b => b.IsActive) : query.OrderBy(b => b.IsActive),
                "createdat" => desc ? query.OrderByDescending(b => b.CreatedAt) : query.OrderBy(b => b.CreatedAt),
                _ => desc ? query.OrderByDescending(b => b.Id) : query.OrderBy(b => b.Id),
            };

            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            int total = await query.CountAsync(cancellationToken);
            List<Branch> items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<Branch?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default) =>
            _context.Set<Branch>().IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        public Task<bool> NameExistsAsync(long tenantId, string name, long? excludeId, CancellationToken cancellationToken = default) =>
            // Global query filter already excludes soft-deleted rows, matching the filtered unique index.
            _context.Set<Branch>().AnyAsync(
                b => b.TenantId == tenantId && b.Name == name && (excludeId == null || b.Id != excludeId),
                cancellationToken);

        public Task<Branch?> GetByNameAsync(long tenantId, string name, CancellationToken cancellationToken = default) =>
      _context.Set<Branch>().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Name == name, cancellationToken);

        public async Task<IReadOnlyDictionary<string, Branch>> GetTenantMapAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            // The global query filter already excludes soft-deleted rows (ConfigureBranch).
            List<Branch> branches = await _context.Set<Branch>()
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            return branches.ToDictionary(b => b.Name, b => b, StringComparer.OrdinalIgnoreCase);
        }
    }
}