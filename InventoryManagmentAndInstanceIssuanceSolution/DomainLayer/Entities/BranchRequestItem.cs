using System.ComponentModel.DataAnnotations;

namespace DomainLayer.Entities
{
    /// <summary>
    /// One requested product line on a <see cref="BranchRequest"/> (ERD §4.2, table
    /// <c>BranchRequestItems</c>; API §4.9).
    /// <para>
    /// No audit block — matches the <see cref="CardTransferProduct"/> precedent: a line belongs
    /// to its parent's append-only-with-status lifecycle rather than having one of its own.
    /// </para>
    /// <para>
    /// Carries two independent counters rather than one (decision D-01), because "how much was
    /// sent" and "how much arrived" answer different questions, and over-fulfilment (decision
    /// Q-03) means neither can be derived from the other:
    /// <see cref="DispatchedQuantity"/> is the cumulative quantity across every transfer
    /// generated for this line, credited at confirm; <see cref="ReceivedQuantity"/> is the
    /// cumulative quantity actually credited to the requesting branch — credited at settlement,
    /// uniformly for a Known-way or an Unknown-way line (Unknown-way Maker-Checker workflow,
    /// supersedes the earlier Unknown Inventory Refactor: a confirm only stages a transfer now,
    /// it never settles one — see <c>BranchRequestService.ConfirmAsync</c> and
    /// <c>BranchRequestFulfilment.ApplyReceiptAsync</c>). Neither counter is ever decremented.
    /// </para>
    /// </summary>
    public class BranchRequestItem
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Owning tenant id. Denormalized plain column with no navigation property of its own —
        /// matches <see cref="CardTransferProduct.TenantId"/>: tenant scoping for this row is
        /// enforced through <see cref="Request"/>, not through an independently navigable FK.
        /// </summary>
        public long TenantId { get; set; }

        /// <summary>
        /// Owning request id (FK → BranchRequests.Id, cascade — a line has no existence apart
        /// from its request).
        /// </summary>
        public long RequestId { get; set; }

        /// <summary>Navigation to the owning request.</summary>
        public BranchRequest Request { get; set; } = null!;

        /// <summary>Requested product id (FK → Products.Id). Unique per request.</summary>
        public long ProductId { get; set; }

        /// <summary>Navigation to the product.</summary>
        public Product Product { get; set; } = null!;

        /// <summary>Quantity asked for. Always greater than zero.</summary>
        public int AskedQuantity { get; set; }

        /// <summary>
        /// Cumulative quantity dispatched across every transfer this request has generated for
        /// this product. Never decremented — a returned or disposed shipment does not reduce it
        /// (decision D-01; see plan risk RK-4, where <see cref="ReceivedQuantity"/> is the
        /// fulfilment measure that matters). Defaults to zero.
        /// <para>
        /// <b>Schema note:</b> not in ERD §4.2; added with decision D-01 (approved). Flagged for
        /// DBA review.
        /// </para>
        /// </summary>
        public int DispatchedQuantity { get; set; }

        /// <summary>
        /// Cumulative quantity actually credited to the requesting branch: the fulfilment measure
        /// that drives <see cref="BranchRequest.RecomputeStatus"/>. Never decremented. Defaults
        /// to zero.
        /// <para>
        /// <b>Schema note:</b> not in ERD §4.2; added with decision D-01 (approved). Flagged for
        /// DBA review.
        /// </para>
        /// </summary>
        public int ReceivedQuantity { get; set; }

        /// <summary>
        /// Credits <paramref name="amount"/> to <see cref="DispatchedQuantity"/>. Called from
        /// <c>BranchRequestService.ConfirmAsync</c> for every generated transfer line matching
        /// this request line, so the increment logic lives in exactly one place rather than a raw
        /// <c>+=</c> repeated across services.
        /// </summary>
        /// <param name="amount">Quantity dispatched. Expected non-negative.</param>
        public void CreditDispatched(int amount) => DispatchedQuantity += amount;

        /// <summary>
        /// Credits <paramref name="amount"/> to <see cref="ReceivedQuantity"/>.
        /// <para>
        /// Called from <c>BranchRequestFulfilment.ApplyReceiptAsync</c> when a generated
        /// transfer settles — the single credit point since the Unknown-way Maker-Checker
        /// workflow, for a Known-way or an Unknown-way line alike.
        /// </para>
        /// <para>
        /// <c>BranchRequestService.ConfirmAsync</c> also holds a call to this method, guarded by
        /// <c>RealQuantityReceived is int</c> — that condition can no longer be true for any line
        /// a confirm stages (every line is pending immediately after staging now), so that call
        /// site is effectively dead today. It is left in place, rather than removed, as the
        /// single correct credit point should some future line shape ever settle inline again.
        /// </para>
        /// </summary>
        /// <param name="amount">Quantity received. Expected non-negative.</param>
        public void CreditReceived(int amount) => ReceivedQuantity += amount;
    }
}
