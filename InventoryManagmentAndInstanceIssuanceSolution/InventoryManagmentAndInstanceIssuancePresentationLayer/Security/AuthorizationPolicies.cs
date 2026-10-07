// InventoryManagmentAndInstanceIssuancePresentationLayer/Security/AuthorizationPolicies.cs
using InfrastructureLayer.Security;
using Microsoft.AspNetCore.Authorization;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Security
{
    /// <summary>
    /// Central definition of the application's authorization policies. The tenant module is
    /// restricted to the bootstrap system admin (API Spec §4.2: "Role: SuperAdmin"), expressed
    /// here as a requirement that the <c>isSystemAdmin</c> claim equals <c>true</c> — there is no
    /// role claim in this single-account-per-tenant model.
    /// </summary>
    public static class AuthorizationPolicies
    {
        /// <summary>Policy name requiring a system-admin token.</summary>
        public const string SystemAdminOnly = "SystemAdminOnly";

        /// <summary>
        /// Policy name requiring a valid Matica Print Agent token (Matica Print Flow). Restricted
        /// to the dedicated <see cref="PrintAgentTokenGenerator.AuthenticationScheme"/> — a normal
        /// tenant/admin session token, even a valid one, is never accepted here, since it is signed
        /// with a different key entirely and was never intended to reach the Printer Agent. The
        /// claim check is a defense-in-depth belt-and-braces addition, not load-bearing on its own.
        /// </summary>
        public const string PrintAgentOnly = "PrintAgentOnly";

        /// <summary>
        /// Policy name requiring a valid reconciliation service token (Matica Print Flow,
        /// reconciliation-credential phase). Restricted to
        /// <see cref="ReconciliationTokenGenerator.AuthenticationScheme"/> — used only by the
        /// background outbox reconciliation job, never by a live print request.
        /// </summary>
        public const string ReconciliationOnly = "ReconciliationOnly";

        /// <summary>
        /// Policy name for <c>print-result</c> specifically: accepts either a live Print Agent
        /// token or a reconciliation service token, since both a live request and a background
        /// retry legitimately call that one action. Both schemes are listed so authentication is
        /// attempted against each; <c>RequireClaim</c>'s built-in support for multiple allowed
        /// values does the "either" check — no custom <c>IAuthorizationHandler</c> needed.
        /// </summary>
        public const string PrintResultAuthorized = "PrintResultAuthorized";

        /// <summary>Registers all application authorization policies.</summary>
        /// <param name="options">The authorization options being configured.</param>
        public static void Register(AuthorizationOptions options)
        {
            options.AddPolicy(SystemAdminOnly, policy =>
                policy.RequireClaim(JwtTokenGenerator.IsSystemAdminClaim, "true", "True"));

            options.AddPolicy(PrintAgentOnly, policy =>
            {
                policy.AuthenticationSchemes.Add(PrintAgentTokenGenerator.AuthenticationScheme);
                policy.RequireClaim(PrintAgentTokenGenerator.PurposeClaim, PrintAgentTokenGenerator.PurposeValue);
            });

            options.AddPolicy(ReconciliationOnly, policy =>
            {
                policy.AuthenticationSchemes.Add(ReconciliationTokenGenerator.AuthenticationScheme);
                policy.RequireClaim(ReconciliationTokenGenerator.PurposeClaim, ReconciliationTokenGenerator.PurposeValue);
            });

            options.AddPolicy(PrintResultAuthorized, policy =>
            {
                policy.AuthenticationSchemes.Add(PrintAgentTokenGenerator.AuthenticationScheme);
                policy.AuthenticationSchemes.Add(ReconciliationTokenGenerator.AuthenticationScheme);
                policy.RequireClaim(
                    PrintAgentTokenGenerator.PurposeClaim,
                    PrintAgentTokenGenerator.PurposeValue,
                    ReconciliationTokenGenerator.PurposeValue);
            });
        }
    }
}