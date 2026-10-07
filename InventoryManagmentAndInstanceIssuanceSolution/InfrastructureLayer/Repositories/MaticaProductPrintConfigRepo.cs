using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="MaticaProductPrintConfiguration"/> — one row per product.</summary>
    public sealed class MaticaProductPrintConfigRepo : GenericRepo<MaticaProductPrintConfiguration, long>, IMaticaProductPrintConfigRepo
    {
        public MaticaProductPrintConfigRepo(AppDbContext context) : base(context) { }

        public Task<MaticaProductPrintConfiguration?> GetByProductIdAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default) =>
            Set.AsNoTracking()
               .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProductId == productId, cancellationToken);

        public Task<MaticaProductPrintConfiguration?> GetByProductIdForUpdateAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default) =>
            // Tracked (no AsNoTracking): the caller (IProductPrintConfigComposer) updates this
            // instance in place, or removes it outright for a printer-family switch (decision Q-08).
            Set.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProductId == productId, cancellationToken);

        public Task<MaticaProductPrintConfiguration?> GetByProductIdIncludingDeletedAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default) =>
            Context.Set<MaticaProductPrintConfiguration>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.ProductId == productId, cancellationToken);
    }
}
