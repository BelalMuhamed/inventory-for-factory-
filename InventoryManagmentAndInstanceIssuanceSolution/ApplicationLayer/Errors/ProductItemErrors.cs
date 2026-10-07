using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Stable, localizable <see cref="Error"/> catalogue for the product-item module.</summary>
    public static class ProductItemErrors
    {
        /// <summary>No item with the given id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error NotFound(long id) =>
            Error.NotFound("ProductItem.NotFound", $"No product item was found with id {id}.")
                 .WithArg(id.ToString());

        /// <summary>
        /// The card is in transit or unassigned (<c>BranchID IS NULL</c>) and cannot be modified
        /// from outside the Transactions module (→ 409). Its quantity is committed to a branch's
        /// hold, so an edit here would desynchronize the stock aggregate from the transfer.
        /// </summary>
        public static Error InTransit(long id) =>
            Error.Conflict("ProductItem.InTransit",
                $"Product item {id} is in transit or unassigned and cannot be modified until the transfer is settled.")
                .WithArg(id.ToString());

        /// <summary>The card has been written off and is in a terminal state (→ 409).</summary>
        public static Error Disposed(long id) =>
            Error.Conflict("ProductItem.Disposed",
                $"Product item {id} has been disposed and can no longer be modified.")
                .WithArg(id.ToString());

        /// <summary>
        /// Disposal was attempted through the generic status-update endpoint (→ 422). Writing a
        /// card off requires a mandatory reason and a disposing branch, neither of which this
        /// payload carries, so it must go through the dispose endpoints instead.
        /// </summary>
        public static Error DisposeNotAllowedHere() =>
            Error.Validation("ProductItem.DisposeNotAllowedHere",
                "Cards cannot be disposed through the status endpoint. Use the dispose endpoint, which requires a reason and a branch.");

        /// <summary>
        /// The supplied PAN is not a well-formed card number after normalization (→ 422). Matica
        /// Print Flow, Backend Call #1.
        /// </summary>
        public static Error InvalidPan() =>
            Error.Validation("ProductItem.InvalidPan", "The supplied card number is not valid.");

        /// <summary>
        /// Matica Print Flow, Backend Call #1: no printable card matches the supplied PAN/product/
        /// branch combination right now (→ 404). Deliberately a single generic outcome — not
        /// "wrong branch" vs. "not available" vs. "not found" — for the same no-existence-leak
        /// reasoning as <see cref="NotFound"/>: a caller holding a Print Agent token should not be
        /// able to distinguish "this card exists elsewhere" from "this card doesn't exist at all."
        /// </summary>
        public static Error NotFoundForPrint() =>
            Error.NotFound("ProductItem.NotFoundForPrint",
                "No printable card was found matching the supplied card number for this product and branch.");

        /// <summary>
        /// Matica Print Flow: the request body's <c>BranchId</c> disagrees with the Print Agent
        /// token's own <c>branchId</c> claim (→ 403). Defense in depth — a leaked or reused token
        /// cannot be redirected to a different branch just by editing the payload.
        /// </summary>
        public static Error PrintFlowScopeMismatch() =>
            Error.Forbidden("ProductItem.PrintFlowScopeMismatch",
                "The requested branch does not match this token's own scope.");
    }
}
