using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Stable, localizable <see cref="Error"/> catalogue for the card-transfer module
    /// (API §4.10).
    /// <para>
    /// Stock-level failures are deliberately absent: insufficient availability, a missing stock
    /// row, and optimistic-concurrency conflicts already have canonical entries in
    /// <see cref="StockErrors"/>, and a transfer hitting one of them should surface the same code
    /// a card status change would. Only failures specific to transfers live here.
    /// </para>
    /// <para>
    /// Card identity never appears in a message. Where a specific card has to be named, the
    /// masked PAN is used — the full PAN is not stored anywhere and the fingerprint must never be
    /// disclosed.
    /// </para>
    /// </summary>
    public static class TransferErrors
    {
        // ---- Actor resolution ---------------------------------------------------------------

        /// <summary>
        /// The caller's principal could not be resolved to a tenant (→ 401). In practice
        /// unreachable behind <c>[Authorize]</c> with a valid tenant token; guarded anyway,
        /// matching every other service's convention.
        /// </summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Transfer.ActorNotResolved", "The acting principal could not be resolved.");

        // ---- Lookup / authorization -------------------------------------------------------

        /// <summary>No transfer with that id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error NotFound(long id) =>
            Error.NotFound("Transfer.NotFound", $"No transfer was found with id {id}.")
                 .WithArg(id.ToString());

        /// <summary>
        /// A system admin attempted to create or settle a transfer (→ 403). Admin access to this
        /// module is read-only (decision Q7); an admin token also carries no tenant id to record
        /// as the creator.
        /// </summary>
        public static Error SystemAdminNotAllowed() =>
            Error.Forbidden("Transfer.SystemAdminNotAllowed",
                "A system administrator cannot create or settle transfers.");

        // ---- Creation: branches -----------------------------------------------------------

        /// <summary>Source and target are the same branch (→ 422).</summary>
        public static Error SameSourceAndTarget() =>
            Error.Validation("Transfer.SameSourceAndTarget",
                "The source and target branches must be different.");

        /// <summary>The branch does not exist, or belongs to another tenant (→ 404).</summary>
        public static Error BranchNotFound(long branchId) =>
            Error.NotFound("Transfer.BranchNotFound", $"No branch was found with id {branchId}.")
                 .WithArg(branchId.ToString());

        /// <summary>A deleted branch cannot take part in a transfer (→ 422).</summary>
        public static Error BranchDeleted(long branchId) =>
            Error.Validation("Transfer.BranchDeleted",
                $"Branch {branchId} is deleted and cannot take part in a transfer.")
                .WithArg(branchId.ToString());

        /// <summary>
        /// The target branch is deactivated (→ 422). Deliberately asymmetric: an inactive
        /// <em>source</em> is allowed, because draining a branch that is being wound down is
        /// exactly what transfers are for, and blocking it would leave that branch impossible to
        /// empty and therefore impossible to delete.
        /// </summary>
        public static Error TargetBranchInactive(long branchId) =>
            Error.Validation("Transfer.TargetBranchInactive",
                $"Branch {branchId} is inactive and cannot receive stock.")
                .WithArg(branchId.ToString());

        // ---- Creation: lines --------------------------------------------------------------

        /// <summary>The request carried no product lines (→ 422).</summary>
        public static Error NoItems() =>
            Error.Validation("Transfer.NoItems", "A transfer must contain at least one product line.");

        /// <summary>The same product appears on more than one line (→ 422).</summary>
        public static Error DuplicateProduct(long productId) =>
            Error.Validation("Transfer.DuplicateProduct",
                $"Product {productId} appears more than once. Combine the quantities into a single line.")
                .WithArg(productId.ToString());

        /// <summary>A transacted quantity was zero or negative (→ 422).</summary>
        public static Error InvalidQuantity(long productId) =>
            Error.Validation("Transfer.InvalidQuantity",
                $"The quantity for product {productId} must be greater than zero.")
                .WithArg(productId.ToString());

        /// <summary>The product does not exist, or belongs to another tenant (→ 404).</summary>
        public static Error ProductNotFound(long productId) =>
            Error.NotFound("Transfer.ProductNotFound", $"No product was found with id {productId}.")
                 .WithArg(productId.ToString());

        /// <summary>The request exceeds the configured per-transfer card limit (→ 422).</summary>
        public static Error TooManyItems(int maximum) =>
            Error.Validation("Transfer.TooManyItems",
                $"A transfer may move at most {maximum} cards.")
                .WithArg(maximum.ToString());

        /// <summary>The supplied note exceeds the stored length (→ 422).</summary>
        public static Error ActionNotesTooLong(int maximum) =>
            Error.Validation("Transfer.ActionNotesTooLong",
                $"The note cannot exceed {maximum} characters.")
                .WithArg(maximum.ToString());

        // ---- Creation: Known-way card selection -------------------------------------------

        /// <summary>A Known-way line supplied no card ids (→ 422, decision Q3).</summary>
        public static Error ItemIdsRequired(long productId) =>
            Error.Validation("Transfer.ItemIdsRequired",
                $"Product {productId} is tracked per card, so the specific cards being transferred must be listed.")
                .WithArg(productId.ToString());

        /// <summary>The number of card ids does not match the transacted quantity (→ 422).</summary>
        public static Error ItemCountMismatch(long productId) =>
            Error.Validation("Transfer.ItemCountMismatch",
                $"The number of cards listed for product {productId} does not match its quantity.")
                .WithArg(productId.ToString());

        /// <summary>
        /// Card ids were supplied for an Unknown-way line (→ 422). Rejected rather than ignored:
        /// silently discarding a caller's explicit selection would leave them believing specific
        /// cards had moved when the system picked its own.
        /// </summary>
        public static Error ItemIdsNotAllowedForUnknown(long productId) =>
            Error.Validation("Transfer.ItemIdsNotAllowedForUnknown",
                $"Product {productId} is not tracked per card, so individual cards cannot be selected for it.")
                .WithArg(productId.ToString());

        /// <summary>The same card id appears more than once in the request (→ 422).</summary>
        public static Error DuplicateItem(long productItemId) =>
            Error.Validation("Transfer.DuplicateItem",
                $"Card {productItemId} is listed more than once.")
                .WithArg(productItemId.ToString());

        /// <summary>The card does not exist, is deleted, or belongs to another tenant (→ 404).</summary>
        public static Error ItemNotFound(long productItemId) =>
            Error.NotFound("Transfer.ItemNotFound", $"No card was found with id {productItemId}.")
                 .WithArg(productItemId.ToString());

        /// <summary>The card is not currently at the source branch (→ 422).</summary>
        public static Error ItemNotAtSourceBranch(string maskedPan) =>
            Error.Validation("Transfer.ItemNotAtSourceBranch",
                $"Card {maskedPan} is not at the source branch.")
                .WithArg(maskedPan);

        /// <summary>The card belongs to a different product than the line it was listed under (→ 422).</summary>
        public static Error ItemProductMismatch(string maskedPan) =>
            Error.Validation("Transfer.ItemProductMismatch",
                $"Card {maskedPan} does not belong to the product it was listed under.")
                .WithArg(maskedPan);

        /// <summary>
        /// The card is not available to move — already in flight, printed, expired, spoiled, or
        /// written off (→ 409).
        /// </summary>
        public static Error ItemNotAvailable(string maskedPan) =>
            Error.Conflict("Transfer.ItemNotAvailable",
                $"Card {maskedPan} is not available to transfer.")
                .WithArg(maskedPan);

        // ---- Settlement -------------------------------------------------------------------

        /// <summary>The transfer has already been settled and cannot be settled again (→ 409).</summary>
        public static Error NotInProgress(long id) =>
            Error.Conflict("Transfer.NotInProgress",
                $"Transfer {id} has already been settled.")
                .WithArg(id.ToString());

        /// <summary>Another operation settled this transfer concurrently (→ 409).</summary>
        public static Error ConcurrencyConflict() =>
            Error.Conflict("Transfer.ConcurrencyConflict",
                "This transfer was modified by another operation. Please reload and retry.");

        /// <summary>The settlement names a product the transfer does not carry (→ 422).</summary>
        public static Error UnknownProductInSettlement(long productId) =>
            Error.Validation("Transfer.UnknownProductInSettlement",
                $"Product {productId} is not part of this transfer.")
                .WithArg(productId.ToString());

        /// <summary>
        /// A product carried by the transfer was left out of the settlement (→ 422). Omission is
        /// not read as "received nothing": a partial receipt has real consequences for stock, so
        /// it must be stated rather than inferred from a missing line.
        /// </summary>
        public static Error MissingProductInSettlement(long productId) =>
            Error.Validation("Transfer.MissingProductInSettlement",
                $"Product {productId} is part of this transfer and must be settled explicitly.")
                .WithArg(productId.ToString());

        /// <summary>Received and disposed quantities are negative, or together exceed what was sent (→ 422).</summary>
        public static Error SettlementQuantityOutOfRange(long productId) =>
            Error.Validation("Transfer.SettlementQuantityOutOfRange",
                $"The received and disposed quantities for product {productId} are invalid for the quantity that was sent.")
                .WithArg(productId.ToString());

        /// <summary>A Known-way line was settled without a per-card outcome for every card (→ 422).</summary>
        public static Error DispositionsRequired(long productId) =>
            Error.Validation("Transfer.DispositionsRequired",
                $"Product {productId} is tracked per card, so every card must be settled individually.")
                .WithArg(productId.ToString());

        /// <summary>The per-card outcomes do not add up to the stated quantities (→ 422).</summary>
        public static Error DispositionCountMismatch(long productId) =>
            Error.Validation("Transfer.DispositionCountMismatch",
                $"The per-card outcomes for product {productId} do not match the received and disposed quantities.")
                .WithArg(productId.ToString());

        /// <summary>A per-card outcome names a card this transfer does not carry (→ 422).</summary>
        public static Error DispositionItemNotInTransfer(long productItemId) =>
            Error.Validation("Transfer.DispositionItemNotInTransfer",
                $"Card {productItemId} is not part of this transfer.")
                .WithArg(productItemId.ToString());

        /// <summary>A card was left pending (→ 422). Settlement must resolve every card.</summary>
        public static Error PendingDispositionNotAllowed(long productItemId) =>
            Error.Validation("Transfer.PendingDispositionNotAllowed",
                $"Card {productItemId} must be settled as received, returned, or disposed.")
                .WithArg(productItemId.ToString());

        // ---- Settlement: Unknown-way remainder (Maker-Checker workflow) -------------------

        /// <summary>
        /// An Unknown-way line was settled with a remainder but no stated resolution (→ 422).
        /// Omission is not read as an implicit choice — the same reasoning as
        /// <see cref="MissingProductInSettlement"/>.
        /// </summary>
        public static Error DifferenceActionRequired(long productId) =>
            Error.Validation("Transfer.DifferenceActionRequired",
                $"Product {productId} was not fully received, so a difference action must be specified.")
                .WithArg(productId.ToString());

        /// <summary>
        /// A difference action was supplied for a line with nothing left to resolve, or for a
        /// Known-way line — whose remainder is always resolved per card instead (→ 422).
        /// </summary>
        public static Error DifferenceActionNotApplicable(long productId) =>
            Error.Validation("Transfer.DifferenceActionNotApplicable",
                $"Product {productId} has no unreceived remainder, or is tracked per card, so a difference action does not apply.")
                .WithArg(productId.ToString());

        /// <summary>The difference action supplied is not a recognized value (→ 422).</summary>
        public static Error InvalidDifferenceAction(long productId) =>
            Error.Validation("Transfer.InvalidDifferenceAction",
                $"The difference action supplied for product {productId} is not recognized.")
                .WithArg(productId.ToString());

        /// <summary>
        /// An Unknown-way line cannot be written off (→ 422) — it moves entitlement only, so
        /// there is no physical card to dispose of. A partial or zero receipt on an Unknown-way
        /// line is resolved with a difference action instead.
        /// </summary>
        public static Error DisposalNotAllowedForUnknown(long productId) =>
            Error.Validation("Transfer.DisposalNotAllowedForUnknown",
                $"Product {productId} is not tracked per card, so it cannot be disposed of. Settle it with a difference action instead.")
                .WithArg(productId.ToString());

        /// <summary>
        /// A remainder has to go back but the branch it came from is no longer usable (→ 409).
        /// The whole settlement is refused rather than partly applied — receiving cards while
        /// stranding the remainder would leave stock unaccounted for.
        /// </summary>
        public static Error ReturnBranchUnavailable(long branchId) =>
            Error.Conflict("Transfer.ReturnBranchUnavailable",
                $"Branch {branchId} is unavailable, so the remaining cards cannot be returned to it.")
                .WithArg(branchId.ToString());

        // NOTE: an earlier draft of this catalogue had a ReturnMustBeSettledInFull error here,
        // forbidding partial receipt on an auto-generated return. Removed — the clarified design
        // treats a return exactly like any other transfer (T5), including partial receipt, so the
        // restriction no longer applies. Chains are unbounded in principle; see the correction on
        // CardTransfer.ParentTransferId's doc comment.

        /// <summary>
        /// A quantity was disposed of but no disposing branch was named (→ 422). Required because
        /// settled cards sit at no branch of their own — there is nothing to infer it from.
        /// </summary>
        public static Error DisposingBranchRequired() =>
            Error.Validation("Transfer.DisposingBranchRequired",
                "A disposing branch is required when any quantity is disposed of.");

        /// <summary>
        /// A write reached the database and failed a check constraint that the service layer's
        /// own validation did not anticipate (→ 409) — for example a settlement race this
        /// method's optimistic-concurrency check did not catch. Surfaced as a clear, logged
        /// conflict rather than an opaque 500, per the same reasoning as
        /// <see cref="StockErrors.ConcurrencyConflict"/>.
        /// </summary>
        public static Error PersistenceConflict() =>
            Error.Conflict("Transfer.PersistenceConflict",
                "The transfer could not be saved because of a conflicting change. Please retry.");

        /// <summary>
        /// The stock aggregate and the card rows disagree — fewer cards are actually available
        /// than the aggregate claims (→ 409). Surfaced rather than silently moving fewer cards,
        /// because quietly transferring the wrong quantity hides the inconsistency instead of
        /// exposing it.
        /// </summary>
        public static Error StockInconsistency(long branchId, long productId) =>
            Error.Conflict("Transfer.StockInconsistency",
                $"Stock for branch {branchId} and product {productId} does not match the cards on record. The transfer was not applied.")
                .WithArg($"{branchId}/{productId}");
    }
}
