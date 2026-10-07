using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.Disposals;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Standalone card disposal (API §4.10, Addendum A §2.4): writing off cards that are sitting
    /// at a branch — damaged, spoiled, or discontinued — outside of any transfer. Disposal that
    /// happens while settling a transfer goes through <see cref="ITransferService"/> instead; this
    /// service is for cards that were never in flight.
    /// </summary>
    public interface IDisposalService
    {
        /// <summary>Lists disposals in the caller's scope.</summary>
        Task<Result<PaginatedResponse<DisposalListItemResponse>>> GetAllAsync(
            DisposalListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Reads one disposal's full detail, including every card written off under it.</summary>
        Task<Result<DisposalDetailResponse>> GetByIdAsync(
            long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes off cards at a branch, named either by explicit id or by per-product quantity
        /// (FIFO selection). Rejects a system-admin caller outright (never permitted to dispose of
        /// cards) and requires a non-empty reason.
        /// </summary>
        Task<Result<DisposalDetailResponse>> CreateAsync(
            DisposeCardsRequest request, CancellationToken cancellationToken = default);
    }
}
