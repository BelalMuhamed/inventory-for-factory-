using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Transfers;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="CardTransfer"/> (ERD §4.3–§4.5).</summary>
    public sealed class CardTransferRepo : GenericRepo<CardTransfer, long>, ICardTransferRepo
    {
        public CardTransferRepo(AppDbContext context) : base(context) { }

        public async Task<(IReadOnlyList<CardTransfer> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, TransferListFilter filter, CancellationToken cancellationToken = default)
        {
            // No soft-delete query filter exists on this entity (it is append-only, ERD §6.5), so
            // rows are visible without IgnoreQueryFilters.
            IQueryable<CardTransfer> query = Set.AsNoTracking()
                .Include(t => t.SourceBranch)
                .Include(t => t.TargetBranch)
                .Include(t => t.Products);

            if (tenantScopeId is long scope)
                query = query.Where(t => t.TenantId == scope);          // tenant caller: forced scope
            else if (filter.TenantId is long requested)
                query = query.Where(t => t.TenantId == requested);      // admin caller: optional filter

            if (filter.Status is { } status)
                query = query.Where(t => t.TransactionStatus == status);

            if (filter.SourceBranchId is long source)
                query = query.Where(t => t.SourceBranchId == source);

            if (filter.TargetBranchId is long target)
                query = query.Where(t => t.TargetBranchId == target);

            // "Everything that touched my branch" (API §4.10 scope note) — either side.
            if (filter.BranchId is long branch)
                query = query.Where(t => t.SourceBranchId == branch || t.TargetBranchId == branch);

            if (filter.ProductId is long product)
                query = query.Where(t => t.Products.Any(p => p.ProductId == product));

            if (filter.Origin is { } origin)
                query = query.Where(t => t.Origin == origin);

            if (filter.ParentTransferId is long parent)
                query = query.Where(t => t.ParentTransferId == parent);

            // Reserved for §4.9. Matches nothing until BranchRequests exists and populates it.
            if (filter.BranchRequestId is long branchRequest)
                query = query.Where(t => t.BranchRequestId == branchRequest);

            if (filter.FromDate is { } from)
                query = query.Where(t => t.CreatedAt >= from);

            if (filter.ToDate is { } to)
                query = query.Where(t => t.CreatedAt <= to);

            bool desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "statuschangedat" => desc ? query.OrderByDescending(t => t.StatusChangedAt) : query.OrderBy(t => t.StatusChangedAt),
                "status" => desc ? query.OrderByDescending(t => t.TransactionStatus) : query.OrderBy(t => t.TransactionStatus),
                "sourcebranchid" => desc ? query.OrderByDescending(t => t.SourceBranchId) : query.OrderBy(t => t.SourceBranchId),
                "targetbranchid" => desc ? query.OrderByDescending(t => t.TargetBranchId) : query.OrderBy(t => t.TargetBranchId),
                _ => desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
            };

            int total = await query.CountAsync(cancellationToken);
            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            List<CardTransfer> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<CardTransfer?> GetDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default)
        {
            IQueryable<CardTransfer> query = Set.AsNoTracking()
                .Include(t => t.SourceBranch)
                .Include(t => t.TargetBranch)
                .Include(t => t.Products).ThenInclude(p => p.Product)
                .Include(t => t.Items).ThenInclude(i => i.ProductItem);

            if (tenantScopeId is long scope)
                query = query.Where(t => t.TenantId == scope);

            return await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        public async Task<CardTransfer?> GetForUpdateAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default)
        {
            IQueryable<CardTransfer> query = Set
                .Include(t => t.Products)
                .Include(t => t.Items).ThenInclude(i => i.ProductItem);

            if (tenantScopeId is long scope)
                query = query.Where(t => t.TenantId == scope);

            return await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        public async Task AddAsync(CardTransfer transfer, CancellationToken cancellationToken = default)
            => await Set.AddAsync(transfer, cancellationToken);

        public async Task<bool> HasInProgressTransferAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().AnyAsync(t =>
                t.TenantId == tenantId &&
                t.TransactionStatus == TransactionStatus.InProgress &&
                (t.SourceBranchId == branchId || t.TargetBranchId == branchId),
                cancellationToken);
    }
}
