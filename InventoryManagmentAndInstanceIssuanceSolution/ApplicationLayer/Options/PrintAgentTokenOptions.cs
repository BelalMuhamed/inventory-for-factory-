namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for the short-lived, narrowly-scoped token minted for the Matica Printer Agent
    /// (Matica Print Flow, backend-validation phase), bound from the <c>"PrintAgentToken"</c>
    /// section via the Options pattern.
    /// <para>
    /// Deliberately a separate signing key from <see cref="JwtOptions.SigningKey"/> — every branch
    /// machine running the Printer Agent can validate tokens against this key, so a compromised
    /// branch machine must only ever be able to forge print-agent-scoped tokens, never a full
    /// tenant or system-admin session signed with the shared key. Supplied via user-secrets
    /// (development) or an environment variable (production); never committed to
    /// <c>appsettings.json</c>, matching <see cref="JwtOptions.SigningKey"/>'s own convention.
    /// </para>
    /// </summary>
    public sealed class PrintAgentTokenOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "PrintAgentToken";

        /// <summary>Token issuer (<c>iss</c>). Distinct from <see cref="JwtOptions.Issuer"/>.</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Intended audience (<c>aud</c>). Distinct from <see cref="JwtOptions.Audience"/>.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Dedicated symmetric signing key (HMAC-SHA256) — never the same value as
        /// <see cref="JwtOptions.SigningKey"/>. Supplied via secrets, never hardcoded.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// Access-token lifetime in minutes. Short by design: this token only needs to live for the
        /// duration of one physical print attempt. Defaults to 5.
        /// </summary>
        public int AccessTokenMinutes { get; set; } = 5;
    }
}
