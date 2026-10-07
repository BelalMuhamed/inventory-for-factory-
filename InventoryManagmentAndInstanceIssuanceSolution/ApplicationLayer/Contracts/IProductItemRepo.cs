using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.ProductItems;
using DomainLayer.Entities;

namespace ApplicationLayer.Contracts
{
    /// <summary>Data-access contract for <see cref="ProductItem"/> (ERD §3.3, API Spec §4.7).</summary>
    public interface IProductItemRepo : IGenericRepo<ProductItem, long>
    {
        /// <summary>Returns a page of product items (product eager-loaded), scoped as for products.</summary>
        Task<(IReadOnlyList<ProductItem> Items, int TotalCount)> GetPagedAsync(
            long? tenantScopeId, ProductItemListFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Reads one item by id (no tracking, product eager-loaded), including deleted rows.</summary>
        Task<ProductItem?> GetByIdIncludingDeletedAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Loads one tracked item by id (product eager-loaded) for in-transaction mutation.</summary>
        Task<ProductItem?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a batch of new items for insertion in one call (Batch Upload Phased Plan,
        /// Phase 3/6) — a single <c>AddRange</c> instead of N individual <c>AddAsync</c> calls.
        /// Does not call <c>SaveChanges</c>; the caller commits via
        /// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
        /// </summary>
        Task AddRangeAsync(IEnumerable<ProductItem> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads every existing item for the tenant whose <see cref="ProductItem.PanFingerprint"/>
        /// is in <paramref name="fingerprints"/>, in one query, keyed by the hex-encoded
        /// fingerprint (<c>Convert.ToHexString</c>) — the one-query re-sight/upsert lookup for
        /// the batch pipeline (§4.8/§6.4). Entities are tracked (not <c>AsNoTracking</c>) so the
        /// caller can mutate Branch/Status in place and commit with a single <c>SaveChanges</c>.
        /// </summary>
        Task<IReadOnlyDictionary<string, ProductItem>> GetExistingByFingerprintsAsync(
            long tenantId, IEnumerable<byte[]> fingerprints, CancellationToken cancellationToken = default);

        /// <summary>
        /// True when at least one card exists for the product, including soft-deleted and disposed
        /// ones. Backs the <c>ProductTransactionWay</c> immutability rule (Transactions §4.10, P6):
        /// once any card has ever been tracked under a given way, the way is frozen — a
        /// soft-deleted card can be restored and a disposed one still appears in history, so
        /// neither may unfreeze it.
        /// </summary>
        /// <param name="tenantId">Owning tenant of the product.</param>
        /// <param name="productId">Product to test.</param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task<bool> ExistsForProductAsync(
            long tenantId, long productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads every card in <paramref name="ids"/> that belongs to the tenant, tracked, keyed
        /// by id (Transactions §4.10, T3). Backs Known-way card selection on create, per-card
        /// settlement on receive, and disposal by explicit id.
        /// <para>
        /// <b>Explicitly filters <c>!IsDeleted</c></b> rather than relying on a query filter —
        /// none exists on this entity (fix F1's scope decision: explicit predicates now, a global
        /// filter as a separate hardening pass). Omitting this check here would let a soft-deleted
        /// card be transferred.
        /// </para>
        /// <para>
        /// A id in <paramref name="ids"/> with no matching row (wrong tenant, wrong id, or
        /// filtered out as deleted) is simply absent from the result — the caller diagnoses which
        /// ids are missing by comparing counts, the same way <c>GetExistingByFingerprintsAsync</c>
        /// is consumed today.
        /// </para>
        /// </summary>
        Task<IReadOnlyDictionary<long, ProductItem>> GetManyForUpdateAsync(
            long tenantId, IEnumerable<long> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects up to <paramref name="take"/> cards at <paramref name="branchId"/> that are
        /// currently <see cref="DomainLayer.Enums.CardStatus.Available"/> for
        /// <paramref name="productId"/>, oldest first, tracked for in-transaction mutation
        /// (Transactions §4.10, T3).
        /// <para>
        /// This is the system's FIFO card-selection for cases where the caller supplies a
        /// quantity rather than specific ids: an Unknown-way transfer line at create time, and a
        /// standalone by-quantity disposal. "Unknown" describes what the caller sees, not what
        /// the system tracks internally — see the note on <c>CardTransferItem</c> for why the
        /// system still needs to know exactly which cards moved.
        /// </para>
        /// <para>
        /// Returns fewer than <paramref name="take"/> items when fewer are actually available —
        /// it never throws and never returns more. A caller that gets back fewer than it asked
        /// for is looking at a live disagreement between <c>Stock.AvailableQuantity</c> and the
        /// card rows backing it, and should fail the operation with a data-inconsistency error
        /// rather than silently moving a smaller quantity than requested.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<ProductItem>> GetAvailableForUpdateAsync(
            long tenantId, long branchId, long productId, int take, CancellationToken cancellationToken = default);
    }
}