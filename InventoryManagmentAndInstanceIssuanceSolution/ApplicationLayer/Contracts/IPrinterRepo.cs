using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Printing;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="Printer"/> (ERD §6.1, Printing Module Q-01). All query
    /// logic is expressed here as named methods; raw predicates never reach the service layer.
    /// The 1:1 Matica extension is deliberately not loaded here — see
    /// <see cref="IMaticaPrinterConfigRepo"/>, kept as its own repository per aggregate.
    /// </summary>
    public interface IPrinterRepo : IGenericRepo<Printer, long>
    {
        /// <summary>
        /// Returns a page of printers. When <paramref name="tenantScopeId"/> is supplied (tenant
        /// caller) results are restricted to that tenant; when <c>null</c> (system admin) the
        /// optional <see cref="PrinterListFilter.TenantId"/> applies instead.
        /// </summary>
        Task<(IReadOnlyList<Printer> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, PrinterListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Finds a printer by id across all tenants, including soft-deleted rows.</summary>
        Task<Printer?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when a non-deleted printer with <paramref name="uniqueNumber"/> already exists
        /// for the tenant (optionally excluding <paramref name="excludeId"/>). Matches the
        /// filtered UNIQUE (TenantId, UniqueNumber) constraint.
        /// </summary>
        Task<bool> UniqueNumberExistsAsync(
            long tenantId, string uniqueNumber, long? excludeId, CancellationToken cancellationToken = default);
    }
}
