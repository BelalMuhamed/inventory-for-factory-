using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Ribbon type reference-table read use case (Printing Module Q-05/Q-19). A small global
    /// lookup list, not tenant-scoped, so unlike <see cref="IPrinterConfigurationService"/> there
    /// is no paging, filtering, or write surface here — rows are seeded, not managed via this API.
    /// </summary>
    public interface IRibbonTypeService
    {
        /// <summary>Returns every ribbon type, for populating a <c>DefaultRibbonTypeId</c> picker.</summary>
        Task<Result<IReadOnlyList<RibbonTypeResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
