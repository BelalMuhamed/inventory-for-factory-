using System;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A standing, revocable service identity for the Matica Printer Agent's background outbox
    /// reconciliation job (Matica Print Flow, reconciliation-credential phase) — deliberately
    /// distinct from the short-lived, user-delegated Print Agent token minted per print attempt.
    /// <para>
    /// One row per Printer Agent instance, scoped to exactly one branch (the same granularity the
    /// Print Agent token itself already uses). <see cref="ClientSecretHash"/> is hashed with the
    /// same <c>IPasswordHasher</c> already used for tenant/admin passwords — the raw secret is
    /// shown to the operator exactly once at provisioning time and is never stored or retrievable
    /// again, the same discipline as a password.
    /// </para>
    /// </summary>
    public sealed class PrintAgentServiceAccount
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant.</summary>
        public long TenantId { get; set; }

        /// <summary>The single branch this service account is scoped to.</summary>
        public long BranchId { get; set; }

        /// <summary>
        /// Public, non-secret identifier presented alongside the secret when requesting a
        /// reconciliation access token. Not a security boundary on its own — the secret is.
        /// </summary>
        public Guid ClientId { get; set; }

        /// <summary>Salted hash of the client secret. The raw secret is never persisted.</summary>
        public string ClientSecretHash { get; set; } = string.Empty;

        /// <summary>Human-readable label for operators (e.g. "Branch 12 Printer Agent"), not used in authorization decisions.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>UTC provisioning time.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// UTC revocation time, or null while active. Checked on every token-mint attempt;
        /// revocation takes effect immediately for new tokens, though an already-minted token
        /// remains valid until it naturally expires (a few minutes, per
        /// <c>ReconciliationTokenOptions.AccessTokenMinutes</c>).
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>UTC time of the most recent successful token mint, for audit visibility.</summary>
        public DateTime? LastUsedAt { get; set; }
    }
}
