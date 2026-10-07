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
    /// <summary>
    /// EF Core repository for <see cref="EvolisPrinterConfiguration"/>. Mirrors
    /// <c>MaticaPrinterConfigRepo</c>'s pattern exactly — confirm against that file directly if
    /// its actual implementation differs from what's assumed here (this was written from the
    /// interface contract and observed call sites, not from viewing MaticaPrinterConfigRepo.cs
    /// itself).
    /// </summary>
    public sealed class EvolisPrinterConfigRepo : GenericRepo<EvolisPrinterConfiguration, long>, IEvolisPrinterConfigRepo
    {
        // Change parameter type from AppContext to AppDbContext to match base class and project conventions
        public EvolisPrinterConfigRepo(AppDbContext context) : base(context) { }

        /// <inheritdoc />
        public Task<EvolisPrinterConfiguration?> GetByPrinterIdAsync(
            long printerId, CancellationToken cancellationToken = default) =>
            Set.FirstOrDefaultAsync(e => e.PrinterId == printerId, cancellationToken);

        /// <inheritdoc />
        public Task<EvolisPrinterConfiguration?> GetByPrinterIdIncludingDeletedAsync(
            long printerId, CancellationToken cancellationToken = default) =>
            Set.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.PrinterId == printerId, cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<long, EvolisPrinterConfiguration>> GetByPrinterIdsAsync(
            IEnumerable<long> printerIds, CancellationToken cancellationToken = default)
        {
            List<long> ids = printerIds.ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<long, EvolisPrinterConfiguration>();
            }

            List<EvolisPrinterConfiguration> rows = await Set
                .AsNoTracking()
                .Where(e => ids.Contains(e.PrinterId))
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(e => e.PrinterId);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<EvolisPrinterConfiguration>> GetByTenantAndBranchAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default) =>
            await Set
                .Include(e => e.Printer)
                .Where(e => e.TenantId == tenantId && e.Printer.BranchId == branchId)
                .ToListAsync(cancellationToken);
    }
}
