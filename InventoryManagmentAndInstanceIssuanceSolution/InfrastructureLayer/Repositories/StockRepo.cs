using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Stocks;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for the materialized <see cref="Stock"/> aggregate (ERD §3.1).</summary>
    public sealed class StockRepo : GenericRepo<Stock, long>, IStockRepo
    {
        public StockRepo(AppDbContext context) : base(context) { }

        public async Task<(IReadOnlyList<Stock> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, StockListFilter filter, CancellationToken cancellationToken = default)
        {
            // Stock has no soft-delete query filter, so rows are visible without IgnoreQueryFilters.
            IQueryable<Stock> query = Set.AsNoTracking()
                .Include(s => s.SettledBranch)
                .Include(s => s.CardType);

            if (tenantScopeId is long scope)
                query = query.Where(s => s.TenantId == scope);          // tenant caller: forced scope
            else if (filter.TenantId is long requested)
                query = query.Where(s => s.TenantId == requested);      // admin caller: optional filter

            if (filter.BranchId is long branch)
                query = query.Where(s => s.BranchId == branch);

            if (filter.ProductId is long product)
                query = query.Where(s => s.ProductId == product);

            if (filter.LowStockOnly is true)
                query = query.Where(s => s.AvailableQuantity <= s.CardType.LowProductThreshold);

            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "available" => desc ? query.OrderByDescending(s => s.AvailableQuantity) : query.OrderBy(s => s.AvailableQuantity),
                "hold" => desc ? query.OrderByDescending(s => s.HoldQuantity) : query.OrderBy(s => s.HoldQuantity),
                "branchid" => desc ? query.OrderByDescending(s => s.BranchId) : query.OrderBy(s => s.BranchId),
                "productid" => desc ? query.OrderByDescending(s => s.ProductId) : query.OrderBy(s => s.ProductId),
                _ => desc ? query.OrderByDescending(s => s.UpdatedAt) : query.OrderBy(s => s.UpdatedAt),
            };

            int total = await query.CountAsync(cancellationToken);
            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            List<Stock> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }
        public async Task<Stock?> GetForUpdateAsync(
            long tenantId, long branchId, long productId, CancellationToken cancellationToken = default)
            => await Set.FindAsync(new object?[] { tenantId, branchId, productId }, cancellationToken);
        public async Task<Stock?> GetByBranchAndProductNameAsync(
            long tenantId, string branchName, string productName, CancellationToken cancellationToken = default)
            => await Set
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.TenantId == tenantId &&
                    s.SettledBranch.Name == branchName &&
                    s.CardType.Name == productName,
                    cancellationToken);

        public async Task AddAsync(Stock stock, CancellationToken cancellationToken = default)
            => await Set.AddAsync(stock, cancellationToken);

        public async Task<Stock> GetOrCreateForUpdateAsync(
            long tenantId, long branchId, long productId, CancellationToken cancellationToken = default)
        {
            Stock? existing = await GetForUpdateAsync(tenantId, branchId, productId, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var stock = new Stock
            {
                TenantId = tenantId,
                BranchId = branchId,
                ProductId = productId,
                AvailableQuantity = 0,
                HoldQuantity = 0
            };

            // Staged only — no SaveChanges here. The caller commits via
            // IUnitOfWork.ExecuteInTransactionAsync alongside the rest of the batch's changes.
            await Set.AddAsync(stock, cancellationToken);
            return stock;
        }

        public async Task<IReadOnlyDictionary<(long BranchId, long ProductId), Stock>> GetManyForUpdateAsync(
            long tenantId, IEnumerable<(long BranchId, long ProductId)> keys, CancellationToken cancellationToken = default)
        {
            List<(long BranchId, long ProductId)> pairs = keys as List<(long, long)> ?? new List<(long, long)>(keys);
            if (pairs.Count == 0)
            {
                return new Dictionary<(long, long), Stock>();
            }

            // EF Core cannot translate a "WHERE (BranchId, ProductId) IN (pairs)" composite
            // predicate directly, so this narrows by the two IN-lists first (a real index hit on
            // the composite PK) and applies the exact pair match client-side. Line counts on a
            // transfer are small — a handful of products at most — so the over-fetch this can
            // cause (a branch/product combination that was requested for a different pair) costs
            // nothing worth avoiding in exchange for one round trip instead of N.
            var branchIds = pairs.Select(p => p.BranchId).Distinct().ToList();
            var productIds = pairs.Select(p => p.ProductId).Distinct().ToList();

            List<Stock> candidates = await Set
                .Where(s => s.TenantId == tenantId && branchIds.Contains(s.BranchId) && productIds.Contains(s.ProductId))
                .ToListAsync(cancellationToken);

            var pairSet = new HashSet<(long, long)>(pairs);
            return candidates
                .Where(s => pairSet.Contains((s.BranchId, s.ProductId)))
                .ToDictionary(s => (s.BranchId, s.ProductId), s => s);
        }

        public async Task<bool> HasNonZeroStockAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().AnyAsync(s =>
                s.TenantId == tenantId && s.BranchId == branchId &&
                (s.AvailableQuantity > 0 || s.HoldQuantity > 0),
                cancellationToken);
    }
}