using DomainLayer.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    /// <summary>Data-access contract for <see cref="Batch"/> (ERD §3.2).</summary>
    public interface IBatchRepo : IGenericRepo<Batch, long>
    {
        /// <summary>
        /// The duplicate-file guard for batch upload (API §4.8): true when a non-deleted batch
        /// with this exact <see cref="Batch.FileMac"/> already exists for the uploading tenant.
        /// Matches the filtered UNIQUE (UploadedByTenantId, FileMac) index (Phase 1) — callers
        /// use this before doing any work, so a duplicate file writes nothing.
        /// </summary>
        /// <param name="tenantId">The uploading tenant (<see cref="Batch.UploadedByTenantId"/>).</param>
        /// <param name="fileMac">SHA-256 fingerprint of the decrypted file content.</param>
        Task<bool> ExistsByFileMacAsync(long tenantId, string fileMac, CancellationToken cancellationToken = default);
    }
}
