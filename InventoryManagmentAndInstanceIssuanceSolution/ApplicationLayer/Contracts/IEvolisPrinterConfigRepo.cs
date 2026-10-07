using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="EvolisPrinterConfiguration"/> (Evolis Integration Plan
    /// §6) — the 1:1 Evolis-only extension of <see cref="Printer"/>. Mirrors
    /// <see cref="IMaticaPrinterConfigRepo"/> exactly; keep the two in sync if either changes.
    /// </summary>
    public interface IEvolisPrinterConfigRepo : IGenericRepo<EvolisPrinterConfiguration, long>
    {
        /// <summary>Finds the Evolis configuration for one printer, or <c>null</c> when the printer is Matica (or unregistered).</summary>
        Task<EvolisPrinterConfiguration?> GetByPrinterIdAsync(long printerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds the Evolis configuration for one printer, including a soft-deleted row (unlike
        /// <see cref="GetByPrinterIdAsync"/>, which respects the standard IsDeleted query
        /// filter). <c>PrinterId</c> is uniquely constrained unconditionally — even a
        /// soft-deleted row still occupies it — so restoring a printer, or self-healing an
        /// inconsistent update, must find that row rather than risk inserting a duplicate that
        /// would fail the unique constraint.
        /// </summary>
        Task<EvolisPrinterConfiguration?> GetByPrinterIdIncludingDeletedAsync(
            long printerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk-loads Evolis configurations for a set of printers in one query, keyed by
        /// <see cref="EvolisPrinterConfiguration.PrinterId"/> — backs
        /// <see cref="IPrinterRepo.GetPagedAsync"/>'s response assembly without an N+1 query per
        /// row. A printer id with no Evolis row (a Matica printer) is simply absent from the result.
        /// </summary>
        Task<IReadOnlyDictionary<long, EvolisPrinterConfiguration>> GetByPrinterIdsAsync(
            IEnumerable<long> printerIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk-loads every registered, non-deleted Evolis configuration for one tenant/branch in
        /// one query — backs supervisor self-registration's three-way reconciliation (Evolis
        /// Integration Plan §7), which needs the full registered set (to report the printers the
        /// Agent's report didn't mention), not just the devices the Agent happened to discover.
        /// Returned as a flat list, not keyed by <see cref="EvolisPrinterConfiguration.DeviceName"/>:
        /// that column is <b>not</b> unique per branch — the schema's own unique index is on
        /// <c>(TenantId, SupervisorHostName, DeviceName)</c>, deliberately allowing the same
        /// device name to repeat across different supervising machines. The caller must group by
        /// name itself and disambiguate by <c>SupervisorHostName</c>.
        /// </summary>
        Task<IReadOnlyList<EvolisPrinterConfiguration>> GetByTenantAndBranchAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default);
    }
}
