using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="EvolisProductPrintConfiguration"/> (ERD §7.1, Printing
    /// Module Q-02/Q-05) — one row per product. <see cref="IGenericRepo{T, TKey}.Remove"/> is the
    /// hard delete decision Q-08 requires on a printer-family switch; no separate method is needed
    /// for it.
    /// </summary>
    public interface IEvolisProductPrintConfigRepo : IGenericRepo<EvolisProductPrintConfiguration, long>
    {
        /// <summary>Reads one product's Evolis configuration (no tracking), or <c>null</c> if the product uses Matica or has none yet.</summary>
        Task<EvolisProductPrintConfiguration?> GetByProductIdAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);

        /// <summary>Loads one tracked row by product id for in-transaction mutation (update, soft delete/restore, or the Q-08 switch's hard delete).</summary>
        Task<EvolisProductPrintConfiguration?> GetByProductIdForUpdateAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads one tracked row by product id, including a soft-deleted row (unlike
        /// <see cref="GetByProductIdForUpdateAsync"/>, which respects the standard IsDeleted
        /// query filter). Restoring a product's configuration needs this — the row being
        /// restored is, by definition, currently soft-deleted, so the filtered lookup would never
        /// find it.
        /// </summary>
        Task<EvolisProductPrintConfiguration?> GetByProductIdIncludingDeletedAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);
    }
}
