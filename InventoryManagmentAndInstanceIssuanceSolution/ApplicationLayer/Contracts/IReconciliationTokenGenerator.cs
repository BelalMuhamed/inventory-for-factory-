using System;

namespace ApplicationLayer.Contracts
{
    /// <summary>A signed reconciliation access token together with its UTC expiry.</summary>
    /// <param name="Token">The serialized, signed JWT.</param>
    /// <param name="ExpiresAt">UTC instant at which the token expires.</param>
    public readonly record struct ReconciliationAccessToken(string Token, DateTime ExpiresAt);

    /// <summary>
    /// Issues short-lived tokens for the Matica Printer Agent's background outbox reconciliation
    /// job, after its <see cref="DomainLayer.Entities.PrintAgentServiceAccount"/> credential has
    /// been verified. Deliberately separate from <see cref="IPrintAgentTokenGenerator"/> — a
    /// user-delegated, per-print-attempt token and a non-interactive service token are different
    /// responsibilities with different signing keys.
    /// </summary>
    public interface IReconciliationTokenGenerator
    {
        /// <summary>Creates a token scoped to the given service account's tenant and branch.</summary>
        ReconciliationAccessToken Create(long tenantId, long branchId);
    }
}
