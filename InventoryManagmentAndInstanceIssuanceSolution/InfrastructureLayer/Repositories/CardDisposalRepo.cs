using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Disposals;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="CardDisposal"/> (Addendum A).</summary>
    public sealed class CardDisposalRepo : GenericRepo<CardDisposal, long>, ICardDisposalRepo
    {
        public CardDisposalRepo(AppDbContext context) : base(context) { }

        public async Task<(IReadOnlyList<CardDisposal> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, DisposalListFilter filter, CancellationToken cancellationToken = default)
        {
            IQueryable<CardDisposal> query = Set.AsNoTracking()
                .Include(d => d.Branch)
                .Include(d => d.Items);

            if (tenantScopeId is long scope)
                query = query.Where(d => d.TenantId == scope);
            else if (filter.TenantId is long requested)
                query = query.Where(d => d.TenantId == requested);

            if (filter.BranchId is long branch)
                query = query.Where(d => d.BranchId == branch);

            if (filter.ProductId is long product)
                query = query.Where(d => d.Items.Any(i => i.ProductItem.ProductId == product));

            if (filter.CardTransferId is long transferId)
                query = query.Where(d => d.CardTransferId == transferId);

            if (filter.TransferRelatedOnly is bool transferRelated)
                query = transferRelated
                    ? query.Where(d => d.CardTransferId != null)
                    : query.Where(d => d.CardTransferId == null);

            if (filter.FromDate is { } from)
                query = query.Where(d => d.DisposedAt >= from);

            if (filter.ToDate is { } to)
                query = query.Where(d => d.DisposedAt <= to);

            bool desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "branchid" => desc ? query.OrderByDescending(d => d.BranchId) : query.OrderBy(d => d.BranchId),
                _ => desc ? query.OrderByDescending(d => d.DisposedAt) : query.OrderBy(d => d.DisposedAt),
            };

            int total = await query.CountAsync(cancellationToken);
            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            List<CardDisposal> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<CardDisposal?> GetDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default)
        {
            IQueryable<CardDisposal> query = Set.AsNoTracking()
                .Include(d => d.Branch)
                .Include(d => d.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product);

            if (tenantScopeId is long scope)
                query = query.Where(d => d.TenantId == scope);

            return await query.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        }

        public async Task AddAsync(CardDisposal disposal, CancellationToken cancellationToken = default)
            => await Set.AddAsync(disposal, cancellationToken);
    }
}
