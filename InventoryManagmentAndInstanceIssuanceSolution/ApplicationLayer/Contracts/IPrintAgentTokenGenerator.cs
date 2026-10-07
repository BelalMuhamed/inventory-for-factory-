using System;

namespace ApplicationLayer.Contracts
{
    /// <summary>A signed Print Agent access token together with its UTC expiry.</summary>
    /// <param name="Token">The serialized, signed JWT.</param>
    /// <param name="ExpiresAt">UTC instant at which the token expires.</param>
    public readonly record struct PrintAgentAccessToken(string Token, DateTime ExpiresAt);

    /// <summary>
    /// Issues short-lived, narrowly-scoped tokens for the Matica Printer Agent (Matica Print Flow).
    /// Deliberately separate from <see cref="IJwtTokenGenerator"/>: this token carries only
    /// <c>tenantId</c>/<c>branchId</c>/<c>printerId</c> claims for one print session, is signed
    /// with its own dedicated key, and is never a substitute for — or derived from — a caller's
    /// real tenant/admin session token.
    /// </summary>
    public interface IPrintAgentTokenGenerator
    {
        /// <summary>
        /// Creates a token scoped to exactly one tenant/branch/printer combination.
        /// </summary>
        /// <param name="tenantId">Owning tenant — the Printer Agent's two backend calls are scoped to this tenant only.</param>
        /// <param name="branchId">Branch the Printer Agent is operating at for this print session.</param>
        /// <param name="printerId">Printer the Printer Agent is driving for this print session.</param>
        PrintAgentAccessToken Create(long tenantId, long branchId, long printerId);
    }
}
