namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for decrypting log/outbox files produced by the Matica Printer Agent (Matica Print
    /// Flow, Super-Admin decryption phase), bound from the <c>"FileEncryption"</c> section.
    /// <para>
    /// This key must be byte-for-byte identical to the Printer Agent's own
    /// <c>FileEncryption:Key</c> — it is a matched pair, the same relationship
    /// <c>PrintAgentTokenOptions.SigningKey</c> already has with the Printer Agent's
    /// <c>PrintAgentAuth:SigningKey</c>. Explicitly not <see cref="JwtOptions.SigningKey"/> and not
    /// derived from it: this key can only ever decrypt log/outbox file content, nothing about a
    /// session or a token, so sharing it with the Printer Agent (which already holds it, by
    /// necessity, to encrypt its own files) does not expand what a compromised branch machine
    /// could do beyond what it can already do to its own files.
    /// </para>
    /// </summary>
    public sealed class FileEncryptionOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "FileEncryption";

        /// <summary>
        /// Base64-encoded AES-256 key (32 raw bytes once decoded) — must match the Printer Agent's
        /// own <c>FileEncryption:Key</c> exactly. Supplied via user-secrets (development) or an
        /// environment variable (production); never committed to <c>appsettings.json</c>.
        /// </summary>
        public string Key { get; set; } = string.Empty;
    }
}
