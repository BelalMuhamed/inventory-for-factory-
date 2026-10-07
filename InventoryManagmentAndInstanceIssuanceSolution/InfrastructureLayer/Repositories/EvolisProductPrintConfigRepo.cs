using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="EvolisProductPrintConfiguration"/> — one row per product.</summary>
    public sealed class EvolisProductPrintConfigRepo : GenericRepo<EvolisProductPrintConfiguration, long>, IEvolisProductPrintConfigRepo
    {
        public EvolisProductPrintConfigRepo(AppDbContext context) : base(context) { }

        public Task<EvolisProductPrintConfiguration?> GetByProductIdAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default) =>
            Set.AsNoTracking()
               .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductId == productId, cancellationToken);

        public Task<EvolisProductPrintConfiguration?> GetByProductIdForUpdateAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default) =>
            // Tracked (no AsNoTracking): the caller (IProductPrintConfigComposer) updates this
            // instance in place, or removes it outright for a printer-family switch (decision Q-08).
            Set.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductId == productId, cancellationToken);

        public Task<EvolisProductPrintConfiguration?> GetByProductIdIncludingDeletedAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default) =>
            Context.Set<EvolisProductPrintConfiguration>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductId == productId, cancellationToken);
    }
}
