using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="MaticaProductPrintConfiguration"/> (ERD §7.2, Printing
    /// Module Q-02/Q-03/Q-04) — one row per product. <see cref="IGenericRepo{T, TKey}.Remove"/> is
    /// the hard delete decision Q-08 requires on a printer-family switch; no separate method is
    /// needed for it.
    /// </summary>
    public interface IMaticaProductPrintConfigRepo : IGenericRepo<MaticaProductPrintConfiguration, long>
    {
        /// <summary>Reads one product's Matica configuration (no tracking), or <c>null</c> if the product uses Evolis or has none yet.</summary>
        Task<MaticaProductPrintConfiguration?> GetByProductIdAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);

        /// <summary>Loads one tracked row by product id for in-transaction mutation (update, soft delete/restore, or the Q-08 switch's hard delete).</summary>
        Task<MaticaProductPrintConfiguration?> GetByProductIdForUpdateAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads one tracked row by product id, including a soft-deleted row (unlike
        /// <see cref="GetByProductIdForUpdateAsync"/>, which respects the standard IsDeleted
        /// query filter). Restoring a product's configuration needs this — the row being
        /// restored is, by definition, currently soft-deleted, so the filtered lookup would never
        /// find it.
        /// </summary>
        Task<MaticaProductPrintConfiguration?> GetByProductIdIncludingDeletedAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);
    }
}
