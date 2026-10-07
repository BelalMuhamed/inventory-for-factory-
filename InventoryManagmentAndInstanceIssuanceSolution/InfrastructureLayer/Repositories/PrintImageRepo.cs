using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories
{
    /// <summary>EF Core repository for <see cref="PrintImage"/>.</summary>
    public sealed class PrintImageRepo : GenericRepo<PrintImage, long>, IPrintImageRepo
    {
        public PrintImageRepo(AppDbContext context) : base(context) { }

        public Task<PrintImage?> GetByOriginalFileNameAsync(
            long tenantId, string originalFileName, CancellationToken cancellationToken = default) =>
            // AsNoTracking: both current callers (the duplicate check in UploadAsync and the
            // name-collision check in ReplaceAsync) only read this row - neither mutates it.
            Set.AsNoTracking().FirstOrDefaultAsync(
                i => i.TenantId == tenantId && i.OriginalFileName == originalFileName, cancellationToken);
    }
}
