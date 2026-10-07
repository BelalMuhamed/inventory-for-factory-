// ApplicationLayer/Errors/TenantErrors.cs
using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Central catalogue of <see cref="Error"/>s for the tenant module. Codes are stable and
    /// machine-readable; the <see cref="ErrorCategory"/> (via the <see cref="Error"/> factory
    /// methods) drives the HTTP status (API Spec §2.5). Messages here are English defaults — the
    /// presentation layer localizes by <see cref="Error.Code"/> via <c>IStringLocalizer</c>
    /// (Messages_en / Messages_ar).
    /// </summary>
    public static class TenantErrors
    {
        // ApplicationLayer/Errors/TenantErrors.cs — example two; apply the pattern to all
        public static Error NotFound(long id) =>
            Error.NotFound("Tenant.NotFound", $"No tenant was found with id {id}.").WithArg(id.ToString());

        public static Error CodeAlreadyExists(string code) =>
            Error.Conflict("Tenant.CodeAlreadyExists", $"A tenant with code '{code}' already exists.").WithArg(code);

      

        /// <summary>The supplied username is already used by another tenant, including a deleted one (→ 409).</summary>
        public static Error UsernameAlreadyExists(string username) =>
            Error.Conflict("Tenant.UsernameAlreadyExists", $"A tenant with username '{username}' already exists.");

        /// <summary>The tenant is already soft-deleted, so it cannot be deleted again (→ 409).</summary>
        public static Error AlreadyDeleted(long id) =>
            Error.Conflict("Tenant.AlreadyDeleted", $"Tenant {id} is already deleted.");

        /// <summary>The tenant is not currently deleted, so it cannot be restored (→ 409).</summary>
        public static Error NotDeleted(long id) =>
            Error.Conflict("Tenant.NotDeleted", $"Tenant {id} is not deleted.");

        // ApplicationLayer/Errors/TenantErrors.cs — add
        /// <summary>The authenticated actor could not be resolved to an active admin (→ 401).</summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Tenant.ActorNotResolved", "The acting administrator could not be resolved.");
    }
}