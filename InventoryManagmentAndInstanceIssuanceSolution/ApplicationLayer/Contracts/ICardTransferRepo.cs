using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Transfers;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Data-access contract for <see cref="CardTransfer"/> and its product/item lines
    /// (ERD §4.3–§4.5, API §4.10).
    /// </summary>
    public interface ICardTransferRepo : IGenericRepo<CardTransfer, long>
    {
        /// <summary>
        /// Returns a page of transfers with branches eager-loaded and product lines included (for
        /// the list projection's line-count and total-quantity columns), scoped as for stock and
        /// product items. A tenant caller passes its <paramref name="tenantScopeId"/>; a system
        /// admin passes <c>null</c> and may filter via <see cref="TransferListFilter.TenantId"/>
        /// (decision Q7: admin access is read-only).
        /// </summary>
        Task<(IReadOnlyList<CardTransfer> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, TransferListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads one transfer by id (no tracking) with branches, product lines and their products,
        /// and item rows with their cards, all eager-loaded — everything
        /// <see cref="ApplicationLayer.DTOs.Transfers.TransferDetailResponse"/> needs in one query.
        /// </summary>
        /// <param name="id">Transfer id.</param>
        /// <param name="tenantScopeId">Tenant scope, or <c>null</c> for a system admin.</param>
        /// <returns>The transfer, or <c>null</c> when it does not exist or is outside scope.</returns>
        Task<CardTransfer?> GetDetailAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads one tracked transfer by id, with its product lines and item rows included, for
        /// in-transaction settlement. Item rows come with their <c>ProductItem</c> also included
        /// and tracked — settlement needs it two ways: to group items by the product line they
        /// belong to (a <c>CardTransferItem</c> carries no <c>ProductId</c> of its own, only a
        /// card id), and to mutate the card's <c>Status</c>/<c>BranchID</c> directly without a
        /// second round trip. <see cref="CardTransfer.RowVersion"/> comes along for the
        /// optimistic-concurrency check the caller performs before mutating.
        /// </summary>
        /// <param name="id">Transfer id.</param>
        /// <param name="tenantScopeId">Tenant scope, or <c>null</c> for a system admin.</param>
        /// <returns>The transfer, or <c>null</c> when it does not exist or is outside scope.</returns>
        Task<CardTransfer?> GetForUpdateAsync(
            long id, long? tenantScopeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a new transfer — header, product lines, and item rows, all reachable through the
        /// aggregate's navigation collections — for insertion in a single call.
        /// </summary>
        Task AddAsync(CardTransfer transfer, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when the branch is the source or target of any transfer still
        /// <see cref="DomainLayer.Enums.TransactionStatus.InProgress"/> (Transactions §4.10, fix
        /// F3). Exists now so <c>BranchService.SoftDeleteAsync</c> (T6) has a ready-made guard: a
        /// branch cannot be deleted while cards are physically in flight to or from it, even if it
        /// currently holds zero settled stock.
        /// </summary>
        Task<bool> HasInProgressTransferAsync(
            long tenantId, long branchId, CancellationToken cancellationToken = default);
    }
}
