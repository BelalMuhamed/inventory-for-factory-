using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="Printer"/>.</summary>
    public sealed class PrinterRepo : GenericRepo<Printer, long>, IPrinterRepo
    {
        private readonly AppDbContext _context;

        public PrinterRepo(AppDbContext context) : base(context) => _context = context;

        public async Task<(IReadOnlyList<Printer> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, PrinterListFilter filter, CancellationToken cancellationToken = default)
        {
            // Ignore the global soft-delete filter so the tri-state IsDeleted can be applied
            // explicitly. Includes Branch (P5 addition) so PrinterResponse.BranchName never
            // costs an extra query per row.
            IQueryable<Printer> query = _context.Set<Printer>().IgnoreQueryFilters().AsNoTracking()
                .Include(p => p.Branch);

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

            if (filter.UsingPrinterType is { } type)
            {
                query = query.Where(p => p.UsingPrinterType == type);
            }

            if (filter.BranchId is long branch)
            {
                query = query.Where(p => p.BranchId == branch);
            }

            // Exact match (not Contains, unlike Product/Branch's Name filters) — this is an
            // identity lookup for one specific device by its known IP/serial, added for the
            // Matica Print Flow, not a browsing filter.
            if (!string.IsNullOrWhiteSpace(filter.UniqueNumber))
            {
                query = query.Where(p => p.UniqueNumber == filter.UniqueNumber);
            }

            bool desc = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (filter.SortBy?.ToLowerInvariant()) switch
            {
                "name" => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "usingprintertype" => desc ? query.OrderByDescending(p => p.UsingPrinterType) : query.OrderBy(p => p.UsingPrinterType),
                "branchid" => desc ? query.OrderByDescending(p => p.BranchId) : query.OrderBy(p => p.BranchId),
                "createdat" => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                _ => desc ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id),
            };

            int page = filter.Page < 1 ? 1 : filter.Page;
            int size = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

            int total = await query.CountAsync(cancellationToken);
            List<Printer> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public Task<Printer?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default) =>
            _context.Set<Printer>().IgnoreQueryFilters().Include(p => p.Branch)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public Task<bool> UniqueNumberExistsAsync(
            long tenantId, string uniqueNumber, long? excludeId, CancellationToken cancellationToken = default) =>
            // Global query filter already excludes soft-deleted rows, matching the filtered
            // UNIQUE (TenantId, UniqueNumber) index (ConfigurePrinterRegistry).
            _context.Set<Printer>().AnyAsync(
                p => p.TenantId == tenantId && p.UniqueNumber == uniqueNumber && (excludeId == null || p.Id != excludeId),
                cancellationToken);
    }
}
