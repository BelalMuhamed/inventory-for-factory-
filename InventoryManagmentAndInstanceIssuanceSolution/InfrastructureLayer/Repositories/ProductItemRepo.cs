using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.ProductItems;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="ProductItem"/> (ERD §3.3).</summary>
    public sealed class ProductItemRepo : GenericRepo<ProductItem, long>, IProductItemRepo
    {
        public ProductItemRepo(AppDbContext context) : base(context) { }

        public async Task<(IReadOnlyList<ProductItem> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, ProductItemListFilter filter, CancellationToken cancellationToken = default)
        {
            IQueryable<ProductItem> query = Set.AsNoTracking().Include(x => x.Product);

            if (tenantScopeId is long scope)
                query = query.Where(x => x.TenantId == scope);
            else if (filter.TenantId is long requested)
                query = query.Where(x => x.TenantId == requested);

            if (filter.IsDeleted is bool deleted)
                query = query.Where(x => x.IsDeleted == deleted);

            if (!string.IsNullOrWhiteSpace(filter.Code))
                query = query.Where(x => x.MaskedPan.Contains(filter.Code));   // substring match on masked PAN (last six digits)

            if (filter.ProductId is long productId)
                query = query.Where(x => x.ProductId == productId);

            if (!string.IsNullOrWhiteSpace(filter.ProductName))
                query = query.Where(x => x.Product.Name.Contains(filter.ProductName));

            if (filter.Status is { } status)
                query = query.Where(x => x.Status == status);

            if (filter.BranchId is long branchId)
                query = query.Where(x => x.BranchID == branchId);

            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "code" => desc ? query.OrderByDescending(x => x.MaskedPan) : query.OrderBy(x => x.MaskedPan),
                "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                "productid" => desc ? query.OrderByDescending(x => x.ProductId) : query.OrderBy(x => x.ProductId),
                "branchid" => desc ? query.OrderByDescending(x => x.BranchID) : query.OrderBy(x => x.BranchID),
                _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            };

            int total = await query.CountAsync(cancellationToken);
            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            List<ProductItem> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<ProductItem?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().Include(x => x.Product).FirstOrDefaultAsync(x => x.ID == id, cancellationToken);

        public async Task<ProductItem?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default)
            => await Set.Include(x => x.Product).FirstOrDefaultAsync(x => x.ID == id, cancellationToken);

        public async Task AddRangeAsync(IEnumerable<ProductItem> items, CancellationToken cancellationToken = default)
            => await Set.AddRangeAsync(items, cancellationToken);

        public async Task<IReadOnlyDictionary<string, ProductItem>> GetExistingByFingerprintsAsync(
            long tenantId, IEnumerable<byte[]> fingerprints, CancellationToken cancellationToken = default)
        {
            List<byte[]> keys = fingerprints as List<byte[]> ?? new List<byte[]>(fingerprints);
            if (keys.Count == 0)
            {
                return new Dictionary<string, ProductItem>();
            }

            // Tracked (no AsNoTracking): the caller mutates Branch/Status in place for the
            // re-sight upsert and commits with a single SaveChanges (§6.4).
            List<ProductItem> items = await Set
                .Where(x => x.TenantId == tenantId && keys.Contains(x.PanFingerprint))
                .ToListAsync(cancellationToken);

            // Keyed by the hex-encoded fingerprint (32-byte HMAC-SHA256 output). No collision
            // handling here: unlike the old masked-value key, a genuine PanFingerprint collision
            // between distinct PANs is cryptographically negligible.
            var map = new Dictionary<string, ProductItem>(StringComparer.Ordinal);
            foreach (ProductItem item in items)
            {
                map[Convert.ToHexString(item.PanFingerprint)] = item;
            }

            return map;
        }

        public async Task<bool> ExistsForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default)
            => await Set
                .AsNoTracking()
                .AnyAsync(x => x.TenantId == tenantId && x.ProductId == productId, cancellationToken);

        public async Task<IReadOnlyDictionary<long, ProductItem>> GetManyForUpdateAsync(
            long tenantId, IEnumerable<long> ids, CancellationToken cancellationToken = default)
        {
            List<long> keys = ids as List<long> ?? new List<long>(ids);
            if (keys.Count == 0)
            {
                return new Dictionary<long, ProductItem>();
            }

            // Tracked (no AsNoTracking): the caller — transfer creation, settlement, or disposal —
            // mutates Status/BranchID in place and commits with a single SaveChanges.
            //
            // !IsDeleted is explicit rather than filter-provided: no query filter exists on this
            // entity (fix F1's scope decision, Transactions §4.10 T0), so a soft-deleted card
            // would otherwise be selectable for transfer.
            List<ProductItem> items = await Set
                .Include(x => x.Product)
                .Where(x => x.TenantId == tenantId && keys.Contains(x.ID) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            return items.ToDictionary(x => x.ID, x => x);
        }

        public async Task<IReadOnlyList<ProductItem>> GetAvailableForUpdateAsync(
            long tenantId, long branchId, long productId, int take, CancellationToken cancellationToken = default)
        {
            if (take <= 0)
            {
                return Array.Empty<ProductItem>();
            }

            // FIFO by CreatedAt: the cards that have sat longest at this branch move first. No
            // AsNoTracking — every caller of this method goes on to mutate the returned rows.
            return await Set
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.BranchID == branchId &&
                    x.ProductId == productId &&
                    x.Status == CardStatus.Available &&
                    !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
    }
}