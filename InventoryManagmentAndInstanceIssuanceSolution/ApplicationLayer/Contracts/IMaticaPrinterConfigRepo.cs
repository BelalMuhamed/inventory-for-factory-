using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="MaticaPrinterConfiguration"/> (ERD §6.2, Printing
    /// Module Q-01) — the 1:1 Matica-only extension of <see cref="Printer"/>.
    /// </summary>
    public interface IMaticaPrinterConfigRepo : IGenericRepo<MaticaPrinterConfiguration, long>
    {
        /// <summary>Finds the Matica configuration for one printer, or <c>null</c> when the printer is Evolis (or unregistered).</summary>
        Task<MaticaPrinterConfiguration?> GetByPrinterIdAsync(long printerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the Matica configuration for one printer, including a soft-deleted row (unlike
        /// <see cref="GetByPrinterIdAsync"/>, which respects the standard IsDeleted query
        /// filter). <c>PrinterId</c> is uniquely constrained unconditionally — even a
        /// soft-deleted row still occupies it — so restoring a printer, or self-healing an
        /// inconsistent update, must find that row rather than risk inserting a duplicate that
        /// would fail the unique constraint.
        /// </summary>
        Task<MaticaPrinterConfiguration?> GetByPrinterIdIncludingDeletedAsync(
            long printerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk-loads Matica configurations for a set of printers in one query, keyed by
        /// <see cref="MaticaPrinterConfiguration.PrinterId"/> — backs
        /// <see cref="IPrinterRepo.GetPagedAsync"/>'s response assembly without an N+1 query per
        /// row. Mirrors <c>IStockRepo.GetManyForUpdateAsync</c>'s bulk-dictionary shape.
        /// A printer id with no Matica row (an Evolis printer) is simply absent from the result.
        /// </summary>
        Task<IReadOnlyDictionary<long, MaticaPrinterConfiguration>> GetByPrinterIdsAsync(
            IEnumerable<long> printerIds, CancellationToken cancellationToken = default);
    }
}
