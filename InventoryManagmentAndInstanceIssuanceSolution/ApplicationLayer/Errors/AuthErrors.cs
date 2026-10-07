// ApplicationLayer/Errors/AuthErrors.cs
using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Auth error catalogue. English defaults here; localized centrally by code.</summary>
    public static class AuthErrors
    {
        /// <summary>Username/password mismatch or inactive principal (→ 401).</summary>
        public static Error InvalidCredentials() =>
            Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password.");

        /// <summary>Refresh token unknown, expired, or revoked (→ 401).</summary>
        public static Error InvalidRefreshToken() =>
            Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");
        /// <summary>The caller's principal could not be resolved (no tenant context / unknown admin) (→ 401).</summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Product.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>
        /// A system-admin token was presented to <c>POST /api/auth/print-agent-token</c> (→ 403).
        /// A Print Agent token is always scoped to one tenant's branch/printer; a system admin has
        /// no tenant context to scope it to.
        /// </summary>
        public static Error PrintAgentTokenRequiresTenant() =>
            Error.Forbidden("Auth.PrintAgentTokenRequiresTenant",
                "A Print Agent token can only be issued for a tenant caller.");

        /// <summary>
        /// The branch supplied to <c>POST /api/auth/print-agent-token</c> does not exist, or does
        /// not belong to the caller's tenant (→ 404, no existence leak across tenants).
        /// </summary>
        public static Error PrintAgentBranchNotFound() =>
            Error.NotFound("Auth.PrintAgentBranchNotFound",
                "No branch was found with the supplied id for this tenant.");

        /// <summary>
        /// The printer supplied to <c>POST /api/auth/print-agent-token</c> does not exist, does not
        /// belong to the caller's tenant, or is not registered at the supplied branch (→ 404).
        /// </summary>
        public static Error PrintAgentPrinterNotFound() =>
            Error.NotFound("Auth.PrintAgentPrinterNotFound",
                "No printer was found with the supplied id at the supplied branch for this tenant.");

        /// <summary>
        /// <c>POST /api/auth/service-token</c> was called with a <c>ClientId</c> that does not
        /// exist, or a <c>ClientSecret</c> that does not match it (→ 401). One code for both cases,
        /// deliberately — the same no-existence-leak discipline as <c>Auth.InvalidCredentials</c>,
        /// so the response never reveals whether a given client id is even provisioned.
        /// </summary>
        public static Error ServiceCredentialInvalid() =>
            Error.Unauthorized("Auth.ServiceCredentialInvalid", "Invalid service credential.");

        /// <summary>
        /// <c>POST /api/auth/service-token</c> was called with a <c>ClientId</c> that exists and a
        /// matching secret, but the account has been revoked (→ 401). Deliberately a distinct code
        /// from <see cref="ServiceCredentialInvalid"/> — an invalid secret and a revoked account
        /// are different operational situations even though both refuse the mint.
        /// </summary>
        public static Error ServiceCredentialRevoked() =>
            Error.Unauthorized("Auth.ServiceCredentialRevoked", "This service credential has been revoked.");

        /// <summary>
        /// The branch supplied to <c>POST /api/auth/service-accounts</c> does not exist, or does
        /// not belong to the supplied tenant (→ 404).
        /// </summary>
        public static Error ServiceAccountBranchNotFound() =>
            Error.NotFound("Auth.ServiceAccountBranchNotFound",
                "No branch was found with the supplied id for the supplied tenant.");

        /// <summary>No service account exists with the supplied id (→ 404), for revoke.</summary>
        public static Error ServiceAccountNotFound() =>
            Error.NotFound("Auth.ServiceAccountNotFound", "No service account was found with the supplied id.");
    }
}