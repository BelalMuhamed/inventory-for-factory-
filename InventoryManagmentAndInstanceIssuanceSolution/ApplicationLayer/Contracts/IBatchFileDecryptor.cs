using DomainLayer.Common;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Decrypts a batch card file (AES-256-GCM). The per-tenant key is derived at call time
    /// from <c>BatchCipherOptions</c> and is never persisted (Batch Upload Phased Plan, Phase 2).
    /// A bad key or a tampered/corrupted file is a clean <see cref="Result{TValue}"/> failure,
    /// not a thrown exception — the GCM authentication tag check does the tamper detection.
    /// <para>
    /// Renamed from <c>IBatchFileCipher</c> in Phase 9.2 when file generation introduced the
    /// opposite operation. Decrypt and encrypt are deliberately separate contracts: the
    /// tenant-facing upload pipeline must be able to decrypt and must never be able to encrypt,
    /// and the system-admin generation pipeline is the mirror image. One implementation
    /// (<c>BatchFileCipher</c>) satisfies both, which is what guarantees the round trip.
    /// </para>
    /// </summary>
    public interface IBatchFileDecryptor
    {
        /// <summary>
        /// Decrypts <paramref name="ciphertext"/> for <paramref name="tenantId"/> and returns the
        /// plaintext file content as UTF-8 text.
        /// </summary>
        /// <param name="tenantId">Tenant the file was issued to; selects the derived key.</param>
        /// <param name="ciphertext">
        /// Raw encrypted file bytes, laid out as [12-byte nonce][16-byte GCM tag][ciphertext] —
        /// the same [nonce][tag][ciphertext] convention <c>BatchFileCipher</c> uses elsewhere in this codebase.
        /// </param>
        /// <returns>
        /// The decrypted plaintext on success, or <c>BatchErrors.DecryptionFailed()</c> on a bad
        /// key, tampered ciphertext, or malformed input.
        /// </returns>
        Result<string> Decrypt(long tenantId, byte[] ciphertext);
    }
}
