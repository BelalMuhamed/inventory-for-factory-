using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Stable, localizable <see cref="Error"/> catalogue for the branch module.</summary>
    public static class BranchErrors
    {
        public static Error NotFound(long id) =>
            Error.NotFound("Branch.NotFound", $"No branch was found with id {id}.").WithArg(id.ToString());

        public static Error NameAlreadyExists(string name) =>
            Error.Conflict("Branch.NameAlreadyExists", $"A branch named '{name}' already exists for this tenant.").WithArg(name);

        public static Error AlreadyDeleted(long id) =>
            Error.Conflict("Branch.AlreadyDeleted", $"Branch {id} is already deleted.");

        public static Error NotDeleted(long id) =>
            Error.Conflict("Branch.NotDeleted", $"Branch {id} is not deleted.");

        /// <summary>A system-admin create call did not supply a target tenant (→ 422).</summary>
        public static Error TenantRequired() =>
            Error.Validation("Branch.TenantRequired", "A target tenant id is required when creating a branch as a system admin.");

        /// <summary>The supplied target tenant does not exist (→ 422).</summary>
        public static Error TargetTenantNotFound(long tenantId) =>
            Error.Validation("Branch.TargetTenantNotFound", $"No tenant exists with id {tenantId}.").WithArg(tenantId.ToString());

        /// <summary>The caller's principal could not be resolved (no tenant context / unknown admin) (→ 401).</summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Branch.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>
        /// The branch holds non-zero stock (→ 409, API §4.5: "Blocked if branch holds non-zero
        /// stock; caller must transfer or write off first"). Transactions §4.10, fix F3 — the spec
        /// note existed before this check enforced it.
        /// </summary>
        public static Error HasStock(long id) =>
            Error.Conflict("Branch.HasStock",
                $"Branch {id} holds non-zero stock and cannot be deleted. Transfer or dispose of it first.")
                .WithArg(id.ToString());

        /// <summary>
        /// The branch is the source or target of a transfer that is still in progress (→ 409).
        /// Deleting it here would strand cards mid-flight with nowhere to be received or returned.
        /// </summary>
        public static Error HasInProgressTransfer(long id) =>
            Error.Conflict("Branch.HasInProgressTransfer",
                $"Branch {id} has a transfer in progress and cannot be deleted until it is settled.")
                .WithArg(id.ToString());
        /// <summary>
        /// The branch has open stock requests and cannot be deleted until they are closed or fulfilled (→ 409).
        /// </summary>
        public static Error HasOpenRequest(long id) =>
            Error.Conflict("Branch.HasOpenRequest",
                $"Branch {id} has open stock requests and cannot be deleted until they are closed or fulfilled.")
                .WithArg(id.ToString());
    }
}