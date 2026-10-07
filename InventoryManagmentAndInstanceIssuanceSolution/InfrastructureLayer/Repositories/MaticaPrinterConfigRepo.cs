using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="MaticaPrinterConfiguration"/>.</summary>
    public sealed class MaticaPrinterConfigRepo : GenericRepo<MaticaPrinterConfiguration, long>, IMaticaPrinterConfigRepo
    {
        public MaticaPrinterConfigRepo(AppDbContext context) : base(context) { }

        public Task<MaticaPrinterConfiguration?> GetByPrinterIdAsync(
            long printerId, CancellationToken cancellationToken = default) =>
            Set.FirstOrDefaultAsync(m => m.PrinterId == printerId, cancellationToken);

        public Task<MaticaPrinterConfiguration?> GetByPrinterIdIncludingDeletedAsync(
            long printerId, CancellationToken cancellationToken = default) =>
            Context.Set<MaticaPrinterConfiguration>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.PrinterId == printerId, cancellationToken);

        public async Task<IReadOnlyDictionary<long, MaticaPrinterConfiguration>> GetByPrinterIdsAsync(
            IEnumerable<long> printerIds, CancellationToken cancellationToken = default)
        {
            List<long> ids = printerIds as List<long> ?? new List<long>(printerIds);
            if (ids.Count == 0)
            {
                return new Dictionary<long, MaticaPrinterConfiguration>();
            }

            List<MaticaPrinterConfiguration> configs = await Set
                .AsNoTracking()
                .Where(m => ids.Contains(m.PrinterId))
                .ToListAsync(cancellationToken);

            return configs.ToDictionary(m => m.PrinterId, m => m);
        }
    }
}
