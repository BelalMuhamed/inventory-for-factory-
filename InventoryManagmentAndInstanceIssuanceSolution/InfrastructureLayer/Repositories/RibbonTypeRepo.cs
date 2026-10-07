using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="RibbonType"/> — a global, non-tenant-scoped reference table.</summary>
    public sealed class RibbonTypeRepo : GenericRepo<RibbonType, long>, IRibbonTypeRepo
    {
        public RibbonTypeRepo(AppDbContext context) : base(context) { }

        public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default) =>
            Set.AnyAsync(r => r.Id == id, cancellationToken);
    }
}
