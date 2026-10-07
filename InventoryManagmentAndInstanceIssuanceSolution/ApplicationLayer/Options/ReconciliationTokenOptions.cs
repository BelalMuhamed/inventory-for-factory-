namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for the short-lived token minted for the Matica Printer Agent's background outbox
    /// reconciliation job (Matica Print Flow, reconciliation-credential phase), bound from the
    /// <c>"ReconciliationToken"</c> section.
    /// <para>
    /// A third dedicated signing key — distinct from both <see cref="JwtOptions.SigningKey"/> and
    /// <see cref="PrintAgentTokenOptions.SigningKey"/>. This token authorizes exactly one thing
    /// (recording a print result during reconciliation), so it gets its own key rather than
    /// reusing either of the other two: a compromised branch machine holding this key can only
    /// ever forge reconciliation-scoped tokens, never a print-agent token and never a tenant/admin
    /// session.
    /// </para>
    /// </summary>
    public sealed class ReconciliationTokenOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "ReconciliationToken";

        /// <summary>Token issuer (<c>iss</c>). Distinct from <see cref="JwtOptions.Issuer"/> and <see cref="PrintAgentTokenOptions.Issuer"/>.</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Intended audience (<c>aud</c>). Distinct from the other two token types' audiences.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Dedicated symmetric signing key (HMAC-SHA256) — never the same value as
        /// <see cref="JwtOptions.SigningKey"/> or <see cref="PrintAgentTokenOptions.SigningKey"/>.
        /// Supplied via secrets, never hardcoded.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// Access-token lifetime in minutes. Short by design, same reasoning as the print-agent
        /// token: this is minted fresh once per reconciliation run, so it never needs to live
        /// longer than that run takes. Defaults to 5.
        /// </summary>
        public int AccessTokenMinutes { get; set; } = 5;
    }
}
