namespace ApplicationLayer.Options
{
    /// <summary>
    /// Strongly-typed JWT settings bound from configuration (<c>"Jwt"</c> section) via the
    /// Options pattern. The signing key is supplied through configuration/user-secrets and is
    /// never hardcoded.
    /// </summary>
    public sealed class JwtOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "Jwt";

        /// <summary>Token issuer (<c>iss</c>).</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Intended audience (<c>aud</c>).</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>Symmetric signing key (HMAC-SHA256). Supplied via secrets, never committed.</summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>Access-token lifetime in minutes (spec suggests ~8 hours = 480).</summary>
        public int AccessTokenMinutes { get; set; } = 480;

        /// <summary>Refresh-token lifetime in days.</summary>
        public int RefreshTokenDays { get; set; } = 7;
    }
}
