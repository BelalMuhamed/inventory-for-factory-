using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="PrintImage"/> (module requirements §5–§7).
    /// </summary>
    public interface IPrintImageRepo : IGenericRepo<PrintImage, long>
    {
        /// <summary>
        /// Finds a tenant's existing upload with this exact original file name, or <c>null</c>
        /// when none exists. Backs duplicate detection on <c>POST /api/print-images</c>
        /// (create-only: a match is reported back as a conflict, nothing is inserted) and the
        /// name-collision check on <c>PUT /api/print-images/{id}</c> (a replacement's new name
        /// must not collide with a <em>different</em> existing row for the same tenant).
        /// </summary>
        Task<PrintImage?> GetByOriginalFileNameAsync(
            long tenantId, string originalFileName, CancellationToken cancellationToken = default);
    }
}
