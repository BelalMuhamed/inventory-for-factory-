using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Stable, localizable <see cref="Error"/> catalogue for the branch stock request module
    /// (API §4.9).
    /// <para>
    /// Failures that belong to the generated transfer itself — Known/Unknown item-id shape,
    /// insufficient stock, card-level conflicts — are deliberately absent: they already have
    /// canonical entries in <see cref="TransferErrors"/> and <see cref="StockErrors"/>, surfaced
    /// via <c>ITransferComposer</c>, and a confirm hitting one of them should read exactly the
    /// same code a direct transfer create would. Only failures specific to the request itself
    /// live here.
    /// </para>
    /// </summary>
    public static class BranchRequestErrors
    {
        // ---- Actor resolution ---------------------------------------------------------------

        /// <summary>
        /// The caller's principal could not be resolved to a tenant (→ 401). In practice
        /// unreachable behind <c>[Authorize]</c> with a valid tenant token; guarded anyway,
        /// matching <see cref="TransferErrors.ActorNotResolved"/>'s convention.
        /// </summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("BranchRequest.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>
        /// A system admin attempted to create, confirm, refuse, or cancel a request (→ 403).
        /// Admin access to this module is read-only (§11), matching the precedent
        /// <see cref="TransferErrors.SystemAdminNotAllowed"/> already set — an admin token also
        /// carries no tenant id to record as <c>ActionTakenByTenantId</c>.
        /// </summary>
        public static Error SystemAdminNotAllowed() =>
            Error.Forbidden("BranchRequest.SystemAdminNotAllowed",
                "A system administrator cannot create, confirm, refuse, or cancel branch requests.");

        // ---- Lookup ---------------------------------------------------------------------------

        /// <summary>No branch request with that id in the caller's scope (→ 404, no existence leak).</summary>
        public static Error NotFound(long id) =>
            Error.NotFound("BranchRequest.NotFound", $"No branch request was found with id {id}.")
                 .WithArg(id.ToString());

        // ---- Creation: request shape ----------------------------------------------------------

        /// <summary>The request carried no product lines (→ 422).</summary>
        public static Error NoItems() =>
            Error.Validation("BranchRequest.NoItems",
                "A branch request must contain at least one product line.");

        /// <summary>An asked quantity was zero or negative (→ 422).</summary>
        public static Error InvalidQuantity(long productId) =>
            Error.Validation("BranchRequest.InvalidQuantity",
                $"The asked quantity for product {productId} must be greater than zero.")
                .WithArg(productId.ToString());

        /// <summary>The same product appears on more than one line (→ 422).</summary>
        public static Error DuplicateProduct(long productId) =>
            Error.Validation("BranchRequest.DuplicateProduct",
                $"Product {productId} appears more than once. Combine the quantities into a single line.")
                .WithArg(productId.ToString());

        /// <summary>The supplied note exceeds the stored length (→ 422).</summary>
        public static Error ActionNotesTooLong(int maximum) =>
            Error.Validation("BranchRequest.ActionNotesTooLong",
                $"The note cannot exceed {maximum} characters.")
                .WithArg(maximum.ToString());

        // ---- Creation: requesting branch -------------------------------------------------------

        /// <summary>The requesting branch does not exist, or belongs to another tenant (→ 404).</summary>
        public static Error BranchNotFound(long branchId) =>
            Error.NotFound("BranchRequest.BranchNotFound", $"No branch was found with id {branchId}.")
                 .WithArg(branchId.ToString());

        /// <summary>A deleted branch cannot raise or fulfil a request (→ 422).</summary>
        public static Error BranchDeleted(long branchId) =>
            Error.Validation("BranchRequest.BranchDeleted",
                $"Branch {branchId} is deleted and cannot take part in a branch request.")
                .WithArg(branchId.ToString());

        /// <summary>
        /// The requesting branch is inactive (→ 422, decision Q-13). A request for an inactive
        /// branch can never be confirmed — the branch would always be rejected as an inactive
        /// transfer target (<see cref="TransferErrors.TargetBranchInactive"/>) — so creation
        /// fails early rather than admitting a request that can only ever sit unconfirmable.
        /// </summary>
        public static Error BranchInactive(long branchId) =>
            Error.Validation("BranchRequest.BranchInactive",
                $"Branch {branchId} is inactive and cannot request stock.")
                .WithArg(branchId.ToString());

        // ---- Creation: products -----------------------------------------------------------------

        /// <summary>The product does not exist, or belongs to another tenant (→ 404).</summary>
        public static Error ProductNotFound(long productId) =>
            Error.NotFound("BranchRequest.ProductNotFound", $"No product was found with id {productId}.")
                 .WithArg(productId.ToString());

        /// <summary>
        /// The requesting branch already has a non-terminal request covering this product
        /// (→ 409, decision Q-11 / D-08). Names the first offending product; the caller adds it
        /// to the existing open request instead of raising a duplicate.
        /// </summary>
        public static Error DuplicateOpenRequest(long productId) =>
            Error.Conflict("BranchRequest.DuplicateOpenRequest",
                $"Product {productId} already has an open request from this branch.")
                .WithArg(productId.ToString());

        // ---- Confirmation -------------------------------------------------------------------

        /// <summary>The confirm carried no transfer plans (→ 422).</summary>
        public static Error NoTransfers() =>
            Error.Validation("BranchRequest.NoTransfers",
                "A confirmation must contain at least one transfer plan.");

        /// <summary>
        /// The request is <c>Fulfilled</c>, <c>Refused</c>, or <c>Cancelled</c> and cannot be
        /// confirmed further (→ 409).
        /// </summary>
        public static Error NotOpenForConfirmation(long id) =>
            Error.Conflict("BranchRequest.NotOpenForConfirmation",
                $"Branch request {id} is not open for confirmation.")
                .WithArg(id.ToString());

        /// <summary>
        /// A confirm plan named the request's own requesting branch as its source (→ 422,
        /// checked before any write — pre-empts the database check
        /// <c>CK_CardsTransferHistory_SourceNotTarget</c> that would otherwise fire once the
        /// generated transfer is staged).
        /// </summary>
        public static Error SourceIsRequestingBranch(long branchId) =>
            Error.Validation("BranchRequest.SourceIsRequestingBranch",
                $"Branch {branchId} is the requesting branch and cannot also be the source of a transfer that fulfils it.")
                .WithArg(branchId.ToString());

        // ---- Refuse / cancel ------------------------------------------------------------------

        /// <summary>
        /// The request is not <c>InProgress</c> or <c>PartiallyConfirmed</c> (→ 409, decision
        /// D-06). Once anything has been received the request cannot be walked back — refuse or
        /// cancel a dispatched-but-unreceived shortfall by settling the transfers it already
        /// generated, not by reopening the request.
        /// </summary>
        public static Error NotOpenForClosure(long id) =>
            Error.Conflict("BranchRequest.NotOpenForClosure",
                $"Branch request {id} is not open for refusal or cancellation.")
                .WithArg(id.ToString());

        // ---- Persistence ------------------------------------------------------------------------

        /// <summary>Another operation modified this request concurrently (→ 409, decision Q-07).</summary>
        public static Error ConcurrencyConflict() =>
            Error.Conflict("BranchRequest.ConcurrencyConflict",
                "This branch request was modified by another operation. Please reload and retry.");

        /// <summary>
        /// A write reached the database and failed a check constraint that the service layer's
        /// own validation did not anticipate (→ 409), surfaced as a clear, logged conflict rather
        /// than an opaque 500 — matching <see cref="TransferErrors.PersistenceConflict"/>.
        /// </summary>
        public static Error PersistenceConflict() =>
            Error.Conflict("BranchRequest.PersistenceConflict",
                "The branch request could not be saved because of a conflicting change. Please retry.");
    }
}
