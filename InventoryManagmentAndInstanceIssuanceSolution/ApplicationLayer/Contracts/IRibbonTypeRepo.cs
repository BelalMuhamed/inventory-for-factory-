using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="RibbonType"/> — a global reference table (Printing
    /// Module Q-05), not tenant-scoped. <see cref="IGenericRepo{T, TKey}.GetAllAsync"/> already
    /// covers listing every ribbon type; the one addition here backs FK validation.
    /// </summary>
    public interface IRibbonTypeRepo : IGenericRepo<RibbonType, long>
    {
        /// <summary>True when a ribbon type with this id exists — backs the FK check on an Evolis print-configuration write.</summary>
        Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
    }
}
