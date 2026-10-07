namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Decrypts log/outbox file content produced by the Matica Printer Agent's own
    /// <c>IFileEncryptionService</c>/<c>AesGcmFileEncryptionService</c> (Matica Print Flow,
    /// file-encryption phase). Deliberately the exact same format and algorithm, not a
    /// second implementation — this exists specifically so the Inventory API can read what the
    /// Printer Agent wrote, not to encrypt anything of its own; the Inventory API never writes a
    /// Printer Agent log or outbox file, only reads and decrypts one on a Super Admin's behalf.
    /// </summary>
    public interface IPrintAgentFileDecryptionService
    {
        /// <summary>
        /// Decrypts a string in the Printer Agent's <c>v1.{nonce}.{tag}.{ciphertext}</c> format.
        /// </summary>
        /// <exception cref="PrintAgentFileFormatException">
        /// <paramref name="encoded"/> is not in the expected format at all (wrong version marker,
        /// wrong segment count, invalid base64) — this string was never produced by the Printer
        /// Agent's encryption service, as opposed to being produced by it and then corrupted.
        /// </exception>
        /// <exception cref="System.Security.Cryptography.CryptographicException">
        /// <paramref name="encoded"/> has the right shape but fails AES-GCM's authentication tag
        /// check — a tampered or corrupted file, not a format problem.
        /// </exception>
        string Decrypt(string encoded);

        /// <summary>
        /// True if <paramref name="value"/> looks like the Printer Agent's encoded format (right
        /// version marker, right segment count) — a cheap, non-throwing shape check, not a
        /// guarantee that <see cref="Decrypt"/> will succeed (the authentication tag can still
        /// fail even when the shape looks right).
        /// </summary>
        bool LooksEncrypted(string value);
    }

    /// <summary><paramref name="value"/> is not in the Printer Agent's encoded file format at all.</summary>
    public sealed class PrintAgentFileFormatException : System.Exception
    {
        public PrintAgentFileFormatException(string message) : base(message) { }
        public PrintAgentFileFormatException(string message, System.Exception innerException) : base(message, innerException) { }
    }
}
