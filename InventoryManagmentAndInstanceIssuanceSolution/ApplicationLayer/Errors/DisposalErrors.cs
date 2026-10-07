using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Stable, localizable <see cref="Error"/> catalogue for card disposal (API §4.10,
    /// Addendum A).
    /// <para>
    /// Disposal is the one operation that destroys quantity and cannot be undone, so its
    /// validation is deliberately strict: an ambiguous or unexplained request is refused rather
    /// than interpreted.
    /// </para>
    /// </summary>
    public static class DisposalErrors
    {
        /// <summary>The caller's principal could not be resolved to a tenant (→ 401).</summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Disposal.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>The same card id appears more than once in the request (→ 422).</summary>
        public static Error DuplicateItem(long productItemId) =>
            Error.Validation("Disposal.DuplicateItem",
                $"Card {productItemId} is listed more than once.")
                .WithArg(productItemId.ToString());

        /// <summary>No disposal with that id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error NotFound(long id) =>
            Error.NotFound("Disposal.NotFound", $"No disposal was found with id {id}.")
                 .WithArg(id.ToString());

        /// <summary>A named card does not exist, is deleted, or belongs to another tenant (→ 404).</summary>
        public static Error CardNotFound(long productItemId) =>
            Error.NotFound("Disposal.CardNotFound", $"No card was found with id {productItemId}.")
                 .WithArg(productItemId.ToString());

        /// <summary>
        /// A write reached the database and failed a check constraint that the service layer's
        /// own validation did not anticipate (→ 409). See <c>TransferErrors.PersistenceConflict</c>
        /// for the identical reasoning — kept as a separate code here rather than reused across
        /// modules, unlike the genuinely shared <see cref="StockErrors.ConcurrencyConflict"/>,
        /// because a disposal-endpoint caller has no reason to learn a "Transfer.*" code.
        /// </summary>
        public static Error PersistenceConflict() =>
            Error.Conflict("Disposal.PersistenceConflict",
                "The disposal could not be saved because of a conflicting change. Please retry.");

        /// <summary>
        /// No reason was supplied (→ 422). Mandatory by design: a write-off without a stated
        /// reason is indistinguishable from stock going missing, which is precisely what the
        /// disposal record exists to rule out.
        /// </summary>
        public static Error ReasonRequired() =>
            Error.Validation("Disposal.ReasonRequired",
                "A reason is required in order to dispose of cards.");

        /// <summary>The reason exceeds the stored length (→ 422).</summary>
        public static Error ReasonTooLong(int maximum) =>
            Error.Validation("Disposal.ReasonTooLong",
                $"The disposal reason cannot exceed {maximum} characters.")
                .WithArg(maximum.ToString());

        /// <summary>A system admin attempted to dispose of cards (→ 403). Never permitted.</summary>
        public static Error SystemAdminNotAllowed() =>
            Error.Forbidden("Disposal.SystemAdminNotAllowed",
                "A system administrator cannot dispose of cards.");

        /// <summary>The disposing branch does not exist, or belongs to another tenant (→ 404).</summary>
        public static Error BranchNotFound(long branchId) =>
            Error.NotFound("Disposal.BranchNotFound", $"No branch was found with id {branchId}.")
                 .WithArg(branchId.ToString());

        /// <summary>The disposing branch is deleted (→ 422).</summary>
        public static Error BranchDeleted(long branchId) =>
            Error.Validation("Disposal.BranchDeleted",
                $"Branch {branchId} is deleted and cannot dispose of cards.")
                .WithArg(branchId.ToString());

        /// <summary>
        /// The named branch is neither the source nor the target of the transfer being settled
        /// (→ 422). With no branch claim in the token this is the closest enforceable form of
        /// "only a party to the transfer may dispose of its cards" (decision Q6).
        /// </summary>
        public static Error BranchNotPartyToTransfer(long branchId) =>
            Error.Validation("Disposal.BranchNotPartyToTransfer",
                $"Branch {branchId} is not involved in this transfer and cannot dispose of its cards.")
                .WithArg(branchId.ToString());

        /// <summary>The request identified no cards to dispose of (→ 422).</summary>
        public static Error NothingToDispose() =>
            Error.Validation("Disposal.NothingToDispose",
                "At least one card must be identified for disposal.");

        /// <summary>
        /// Both an explicit card list and a per-product quantity list were supplied (→ 422).
        /// Refused rather than resolved by precedence: guessing which the caller meant risks
        /// destroying the wrong cards.
        /// </summary>
        public static Error SelectionAmbiguous() =>
            Error.Validation("Disposal.SelectionAmbiguous",
                "Specify either the cards to dispose of or the quantities per product, not both.");

        /// <summary>The card has already been written off (→ 409).</summary>
        public static Error AlreadyDisposed(string maskedPan) =>
            Error.Conflict("Disposal.AlreadyDisposed",
                $"Card {maskedPan} has already been disposed.")
                .WithArg(maskedPan);

        /// <summary>
        /// The card has been printed and issued to an end customer (→ 409). It has left inventory
        /// through a different door and is no longer the platform's to write off.
        /// </summary>
        public static Error NotDisposable(string maskedPan) =>
            Error.Conflict("Disposal.NotDisposable",
                $"Card {maskedPan} has been issued and can no longer be disposed.")
                .WithArg(maskedPan);

        /// <summary>The card is not at the branch performing the disposal (→ 422).</summary>
        public static Error CardNotAtBranch(string maskedPan) =>
            Error.Validation("Disposal.CardNotAtBranch",
                $"Card {maskedPan} is not held by the branch performing this disposal.")
                .WithArg(maskedPan);

        /// <summary>
        /// The card is committed to an in-flight transfer and cannot be written off outside it
        /// (→ 409). Disposing of it here would leave the transfer's hold quantity backed by
        /// nothing; it must be disposed of as part of settling that transfer.
        /// </summary>
        public static Error CardInTransfer(string maskedPan) =>
            Error.Conflict("Disposal.CardInTransfer",
                $"Card {maskedPan} is part of an active transfer and must be disposed of when that transfer is settled.")
                .WithArg(maskedPan);

        /// <summary>The branch does not hold enough available cards of that product (→ 409).</summary>
        public static Error InsufficientAvailable(long branchId, long productId) =>
            Error.Conflict("Disposal.InsufficientAvailable",
                $"Branch {branchId} does not hold enough available cards of product {productId} to dispose of.")
                .WithArg($"{branchId}/{productId}");

        /// <summary>A disposal quantity was zero or negative (→ 422).</summary>
        public static Error InvalidQuantity(long productId) =>
            Error.Validation("Disposal.InvalidQuantity",
                $"The disposal quantity for product {productId} must be greater than zero.")
                .WithArg(productId.ToString());

        /// <summary>The same product appears on more than one disposal line (→ 422).</summary>
        public static Error DuplicateProduct(long productId) =>
            Error.Validation("Disposal.DuplicateProduct",
                $"Product {productId} appears more than once. Combine the quantities into a single line.")
                .WithArg(productId.ToString());
    }
}
