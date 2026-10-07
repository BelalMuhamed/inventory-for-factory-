using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Products;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="Product"/>.</summary>
    public sealed class ProductRepo : GenericRepo<Product, long>, IProductRepo
    {
        private readonly AppDbContext _context;

        public ProductRepo(AppDbContext context) : base(context) => _context = context;

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, ProductListFilter filter, CancellationToken cancellationToken = default)
        {
            // Ignore the global soft-delete filter so the tri-state IsDeleted can be applied explicitly.
            IQueryable<Product> query = _context.Set<Product>().IgnoreQueryFilters().AsNoTracking();

            if (tenantScopeId is long scope)
            {
                query = query.Where(p => p.TenantId == scope);          // tenant caller: forced scope
            }
            else if (filter.TenantId is long requested)
            {
                query = query.Where(p => p.TenantId == requested);      // admin caller: optional filter
            }

            if (filter.IsDeleted is bool deleted)
            {
                query = query.Where(p => p.IsDeleted == deleted);
            }

            if (filter.ActivationStatus is { } status)
            {
                query = query.Where(p => p.ActivationStatus == status);
            }

            if (filter.ProductTransactionWay is { } way)
            {
                query = query.Where(p => p.ProductTransactionWay == way);
            }

            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                query = query.Where(p => p.Name.Contains(filter.Name));
            }

            // TODO (stock seam): apply filter.LowStockOnly once the Stock aggregate (ERD §3.1) exists —
            // join Stock per (TenantId, ProductId) and keep products whose summed AvailableQuantity
            // is at or below LowProductThreshold (API §4.6). Ignored until then.

            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "name" => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "activationstatus" => desc ? query.OrderByDescending(p => p.ActivationStatus) : query.OrderBy(p => p.ActivationStatus),
                "lowproductthreshold" => desc ? query.OrderByDescending(p => p.LowProductThreshold) : query.OrderBy(p => p.LowProductThreshold),
                "createdat" => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                _ => desc ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id),
            };

            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            int total = await query.CountAsync(cancellationToken);
            List<Product> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<Product?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default)
            => await _context.Set<Product>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public async Task<bool> NameExistsAsync(long tenantId, string name, long? excludeId, CancellationToken cancellationToken = default)
            => await _context.Set<Product>()
                .IgnoreQueryFilters()
                .AnyAsync(p => p.TenantId == tenantId
                            && !p.IsDeleted
                            && p.Name == name
                            && (excludeId == null || p.Id != excludeId), cancellationToken);

        public async Task<Product?> GetByNameAsync(long tenantId, string name, CancellationToken cancellationToken = default)
      => await _context.Set<Product>()
          .IgnoreQueryFilters()
          .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Name == name, cancellationToken);

        public async Task<IReadOnlyDictionary<string, Product>> GetTenantMapAsync(long tenantId, CancellationToken cancellationToken = default)
        {
            // The global query filter already excludes soft-deleted rows (ConfigureProduct).
            List<Product> products = await _context.Set<Product>()
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            return products.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        }
    }
}
