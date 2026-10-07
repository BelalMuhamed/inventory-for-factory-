namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for the PAN fingerprint HMAC (card identity/dedup hash), bound from the
    /// <c>"PanHash"</c> section. Deliberately a separate master secret from
    /// <see cref="BatchCipherOptions"/> (key separation): a compromised or rotated file-cipher
    /// key must never also compromise PAN identity hashing, and vice versa. Supplied via
    /// user-secrets/environment — never committed, never persisted.
    /// </summary>
    public sealed class PanHashOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "PanHash";

        /// <summary>Master secret every tenant's derived HMAC key is built from (PBKDF2). Supplied via secrets.</summary>
        public string MasterSecret { get; set; } = string.Empty;

        /// <summary>
        /// Base64 salt for key derivation, combined with the tenant id so each tenant gets a
        /// distinct HMAC key from the same master secret. Supplied via secrets; must stay
        /// stable, or every previously computed <c>PanFingerprint</c> becomes unmatchable.
        /// </summary>
        public string Salt { get; set; } = string.Empty;
    }
}
