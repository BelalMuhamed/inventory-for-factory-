namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for batch-file decryption (AES-256-GCM), bound from the <c>"BatchCipher"</c>
    /// section. A single master secret is supplied via user-secrets/environment — never
    /// committed, never persisted — and <see cref="BatchFileCipher"/> derives a distinct
    /// per-tenant key from it at call time (Batch Upload Phased Plan, Phase 2: "key from a
    /// per-tenant secret ... never persisted").
    /// </summary>
    public sealed class BatchCipherOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "BatchCipher";

        /// <summary>Master secret every tenant's derived key is built from (PBKDF2). Supplied via secrets.</summary>
        public string MasterSecret { get; set; } = string.Empty;

        /// <summary>
        /// Base64 salt for key derivation, combined with the tenant id so each tenant gets a
        /// distinct key from the same master secret. Supplied via secrets; must stay stable, or
        /// previously-uploaded files become undecryptable.
        /// </summary>
        public string Salt { get; set; } = string.Empty;
    }
}
