namespace invetoryBackGroundServices.Options
{
    /// <summary>
    /// Settings for validating the short-lived Print Agent token Angular presents to this service,
    /// bound from the <c>"PrintAgentAuth"</c> section.
    /// <para>
    /// <b>Critical:</b> <see cref="Issuer"/>, <see cref="Audience"/>, and especially
    /// <see cref="SigningKey"/> must be the exact same values configured on the Inventory API side
    /// under <c>PrintAgentToken:Issuer</c> / <c>:Audience</c> / <c>:SigningKey</c> — one service
    /// mints this token, the other only validates it, and they only agree on what a valid token
    /// looks like if both sides share the same secret. This is deliberately a separate secret from
    /// the Inventory API's own tenant/admin JWT signing key (see the Inventory API's
    /// <c>PrintAgentTokenGenerator</c> doc comment for why): this service only ever needs to be
    /// able to validate print-agent-scoped tokens, never anything that could forge a real tenant
    /// session.
    /// </para>
    /// </summary>
    public sealed class PrintAgentAuthOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "PrintAgentAuth";

        /// <summary>Expected token issuer (<c>iss</c>) — must match the Inventory API's <c>PrintAgentToken:Issuer</c>.</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Expected token audience (<c>aud</c>) — must match the Inventory API's <c>PrintAgentToken:Audience</c>.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Shared symmetric signing key (HMAC-SHA256) — must be byte-for-byte identical to the
        /// Inventory API's <c>PrintAgentToken:SigningKey</c>. Supplied via secrets/environment
        /// variable, never committed to <c>appsettings.json</c>.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;
    }
}
