using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.DTOs.ProductItems;
using DomainLayer.Common;

namespace ApplicationLayer.ServicesContracts
{
    /// <summary>Product-item use cases (API Spec §4.7).</summary>
    public interface IProductItemService
    {
        /// <summary>Returns a page of product items the caller may see.</summary>
        Task<Result<PaginatedResponse<ProductItemResponse>>> GetAllAsync(
            ProductItemListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Returns a single product item by id, scoped to the caller.</summary>
        Task<Result<ProductItemResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates status, holder name and notes, and transactionally recomputes the branch stock
        /// aggregate when the status crosses the Available boundary.
        /// </summary>
        Task<Result<ProductItemResponse>> UpdateAsync(
            long id, UpdateProductItemRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Matica Print Flow, Backend Call #1 (called by the Printer Agent right after
        /// <c>ReadMAG</c>): resolves the exact physical card matching <paramref name="request"/>'s
        /// PAN via its identity fingerprint (never a substring match on the display PAN), then
        /// validates it is printable — branching on the product's <c>ProductTransactionWay</c>
        /// (Known: the card's own branch/status; Unknown: the branch's aggregate stock — never
        /// FIFO card selection, since the caller already identifies one specific physical card).
        /// Read-only: nothing is mutated here.
        /// </summary>
        Task<Result<ResolveForPrintResponse>> ResolveForPrintAsync(
            ResolveForPrintRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Matica Print Flow, Backend Call #2 (called by the Printer Agent right after
        /// <c>EjectCard</c>): records the physical outcome of one print attempt and transactionally
        /// updates stock. For a Known-way card this reuses the same Available-boundary stock delta
        /// as <see cref="UpdateAsync"/>. For an Unknown-way card — which sits with a null branch
        /// right up until this call, so <see cref="UpdateAsync"/> cannot handle it — this assigns
        /// the branch for the first time and decrements <c>Stock.AvailableQuantity</c> directly,
        /// since that card's availability was already counted in the aggregate rather than in its
        /// own per-item status. Safely retryable: if the item is already at the requested
        /// branch/status, this is a no-op success rather than re-applying the stock delta — a
        /// deliberately lightweight idempotency check, not a persisted key table.
        /// </summary>
        Task<Result<ProductItemResponse>> RecordPrintResultAsync(
            long productItemId, RecordPrintResultRequest request, CancellationToken cancellationToken = default);
    }
}