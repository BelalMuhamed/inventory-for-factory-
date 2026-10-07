using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.BranchRequests;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>
    /// EF Core repository for <see cref="BranchRequest"/> and its lines (ERD §4.1–§4.2,
    /// API §4.9).
    /// </summary>
    public sealed class BranchRequestRepo : GenericRepo<BranchRequest, long>, IBranchRequestRepo
    {
        /// <summary>
        /// The three terminal statuses (decision D-08: "any status other than Fulfilled, Refused,
        /// or Cancelled" defines an open request). Defined once here and reused by every guard
        /// method below — <see cref="GetOpenProductIdsForBranchAsync"/>,
        /// <see cref="HasOpenRequestForBranchAsync"/>, and
        /// <see cref="HasOpenRequestForProductAsync"/> — so the definition of "open" cannot drift
        /// between them. EF Core translates <c>array.Contains(column)</c> to a SQL <c>IN</c>
        /// clause, so this composes cleanly with each method's own additional filters.
        /// </summary>
        private static readonly BranchRequestStatus[] TerminalStatuses =
        {
            BranchRequestStatus.Fulfilled,
            BranchRequestStatus.Refused,
            BranchRequestStatus.Cancelled,
        };

        public BranchRequestRepo(AppDbContext context) : base(context) { }

        public async Task<(IReadOnlyList<BranchRequest> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, StockRequestListFilter filter, CancellationToken cancellationToken = default)
        {
            // No soft-delete query filter exists on this entity (decision Q-09, no soft delete),
            // so rows are visible without IgnoreQueryFilters — matching CardTransferRepo.
            IQueryable<BranchRequest> query = Set.AsNoTracking()
                .Include(r => r.RequestingBranch)
                .Include(r => r.Items);

            if (tenantScopeId is long scope)
                query = query.Where(r => r.TenantId == scope);              // tenant caller: forced scope
            else if (filter.TenantId is long requested)
                query = query.Where(r => r.TenantId == requested);          // admin caller: optional filter

            if (filter.Status is { } status)
                query = query.Where(r => r.RequestStatus == status);

            if (filter.RequestingBranchId is long branch)
                query = query.Where(r => r.RequestingBranchId == branch);

            if (filter.ProductId is long product)
                query = query.Where(r => r.Items.Any(i => i.ProductId == product));

            if (filter.FromDate is { } from)
                query = query.Where(r => r.RequestDateTime >= from);

            if (filter.ToDate is { } to)
                query = query.Where(r => r.RequestDateTime <= to);

            bool desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "status" => desc ? query.OrderByDescending(r => r.RequestStatus) : query.OrderBy(r => r.RequestStatus),
                "requestingbranchid" => desc
                    ? query.OrderByDescending(r => r.RequestingBranchId)
                    : query.OrderBy(r => r.RequestingBranchId),
                _ => desc ? query.OrderByDescending(r => r.RequestDateTime) : query.OrderBy(r => r.RequestDateTime),
            };

            int total = await query.CountAsync(cancellationToken);
            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            List<BranchRequest> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<BranchRequest?> GetDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default)
        {
            IQueryable<BranchRequest> query = Set.AsNoTracking()
                .Include(r => r.RequestingBranch)
                .Include(r => r.Items).ThenInclude(i => i.Product);

            if (tenantScopeId is long scope)
                query = query.Where(r => r.TenantId == scope);

            return await query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<BranchRequest?> GetForUpdateAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default)
        {
            // Tracked (no AsNoTracking): confirm/refuse/cancel mutate this instance directly, and
            // RowVersion must come back live for the optimistic-concurrency check (decision Q-07).
            IQueryable<BranchRequest> query = Set
                .Include(r => r.Items).ThenInclude(i => i.Product);

            if (tenantScopeId is long scope)
                query = query.Where(r => r.TenantId == scope);

            return await query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task AddAsync(BranchRequest request, CancellationToken cancellationToken = default)
            => await Set.AddAsync(request, cancellationToken);

        public async Task<IReadOnlyCollection<long>> GetOpenProductIdsForBranchAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking()
                .Where(r =>
                    r.TenantId == tenantId &&
                    r.RequestingBranchId == branchId &&
                    !TerminalStatuses.Contains(r.RequestStatus))
                .SelectMany(r => r.Items.Select(i => i.ProductId))
                .Distinct()
                .ToListAsync(cancellationToken);

        public async Task<bool> HasOpenRequestForBranchAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().AnyAsync(r =>
                r.TenantId == tenantId &&
                r.RequestingBranchId == branchId &&
                !TerminalStatuses.Contains(r.RequestStatus),
                cancellationToken);

        public async Task<bool> HasOpenRequestForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().AnyAsync(r =>
                r.TenantId == tenantId &&
                !TerminalStatuses.Contains(r.RequestStatus) &&
                r.Items.Any(i => i.ProductId == productId),
                cancellationToken);
    }
}
