using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// The narrow seam <c>TransferService</c> uses to credit a branch request's fulfilment when
    /// one of its generated transfers settles (API §4.9, decisions D-01/D-04). Deliberately tiny
    /// (interface segregation) so <c>TransferService</c> never has to learn the rest of the §4.9
    /// service surface just to report a receipt back.
    /// <para>
    /// <b>Scope, post the Unknown-way Maker-Checker workflow:</b> every line a confirm generates,
    /// Known or Unknown, now settles later via <c>ReceiveAsync</c>/<c>DisposeAsync</c> — a confirm
    /// only stages transfers (<c>ITransferComposer.StageAsync</c>), it never settles one. This
    /// interface is therefore the single credit path for both ways; the "settled inline at
    /// confirm" case this comment used to describe no longer exists for any line shape.
    /// </para>
    /// </summary>
    public interface IBranchRequestFulfilment
    {
        /// <summary>
        /// Credits received quantities to a request's lines and recomputes its status. Called
        /// from inside the settlement transaction — stages only, never saves; the caller's own
        /// <c>SaveChangesAsync</c> commits it alongside the settlement it belongs to.
        /// <para>
        /// Silently no-ops when <paramref name="branchRequestId"/> does not resolve to a request,
        /// or when <paramref name="targetBranchId"/> does not match the request's requesting
        /// branch (decision D-04) — an auto-generated return transfer carries the same
        /// <paramref name="branchRequestId"/> as its parent but heads away from the requesting
        /// branch, so it must never credit.
        /// </para>
        /// </summary>
        /// <param name="branchRequestId">The request the settled transfer fulfils.</param>
        /// <param name="targetBranchId">The settled transfer's target branch.</param>
        /// <param name="receivedByProductId">
        /// Quantity actually received per product, keyed by product id. A product with no
        /// matching line on the request is silently ignored (decision D-05) — only quantities
        /// against products the request actually asked for are credited.
        /// </param>
        /// <param name="cancellationToken">Token to observe while awaiting the operation.</param>
        Task ApplyReceiptAsync(
            long branchRequestId,
            long targetBranchId,
            IReadOnlyDictionary<long, int> receivedByProductId,
            CancellationToken cancellationToken = default);
    }
}
