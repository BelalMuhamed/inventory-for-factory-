using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="Batch"/>.</summary>
    public class BatchRepo : GenericRepo<Batch, long>, IBatchRepo
    {
        public BatchRepo(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByFileMacAsync(long tenantId, string fileMac, CancellationToken cancellationToken = default)
            // Batch has no global soft-delete query filter, so IsDeleted is checked explicitly
            // to match the filtered UNIQUE (UploadedByTenantId, FileMac) index (Phase 1).
            => await Set.AnyAsync(
                b => !b.IsDeleted && b.UploadedByTenantId == tenantId && b.FileMac == fileMac,
                cancellationToken);
    }
}
