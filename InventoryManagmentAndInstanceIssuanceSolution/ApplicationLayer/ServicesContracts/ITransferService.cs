using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Transfers;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Card-transfer use cases (API §4.10): create a direct transfer, settle it through the
    /// disposition model, or write off everything it still carries. Reads are available to a
    /// system admin across tenants (Q7); every write requires an authenticated tenant and rejects
    /// a system-admin caller outright, since a transfer's <c>CreatedByTenantId</c> has nowhere to
    /// point for an admin token.
    /// </summary>
    public interface ITransferService
    {
        /// <summary>Lists transfers in the caller's scope (API §4.10 list and history endpoints).</summary>
        Task<Result<PaginatedResponse<TransferListItemResponse>>> GetAllAsync(
            TransferListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Reads one transfer's full detail, including product lines and card-level items.</summary>
        Task<Result<TransferDetailResponse>> GetByIdAsync(
            long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a direct transfer (no underlying branch request): validates both branches and
        /// every product line, selects the cards leaving the source (explicitly for Known-way
        /// lines, FIFO for Unknown-way), and moves the source's stock from Available into Hold.
        /// </summary>
        Task<Result<TransferDetailResponse>> CreateAsync(
            CreateTransferRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Settles an in-progress transfer against a per-product disposition: received, disposed,
        /// or (implicitly, for whatever is left over) returned. A non-empty remainder spawns a new
        /// transfer back to the original source, which that branch settles in its own right —
        /// through this same method, since a return has no separate workflow.
        /// </summary>
        Task<Result<SettleTransferResult>> ReceiveAsync(
            long id, ReceiveTransferRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes off everything an in-progress transfer still carries, in one step, without
        /// receiving any of it. Equivalent to calling <see cref="ReceiveAsync"/> with every line's
        /// full quantity disposed of and nothing received.
        /// </summary>
        Task<Result<SettleTransferResult>> DisposeAsync(
            long id, DisposeTransferRequest request, CancellationToken cancellationToken = default);
    }
}
