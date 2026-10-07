using DomainLayer.Common;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Encrypts a batch card file for delivery to a tenant (AES-256-GCM), the inverse of
    /// <see cref="IBatchFileDecryptor"/> (Card File Generation, Phase 9.2). Used only by the
    /// system-admin generation pipeline.
    /// <para>
    /// Kept separate from <see cref="IBatchFileDecryptor"/> rather than merged into one
    /// <c>ICipher</c>: the upload pipeline has no business being able to encrypt, and on a
    /// security-sensitive type the narrower dependency is worth the extra interface. Both are
    /// implemented by the same <c>BatchFileCipher</c>, so both directions share one key-derivation
    /// routine — which is precisely what makes the round trip safe to rely on.
    /// </para>
    /// </summary>
    public interface IBatchFileEncryptor
    {
        /// <summary>
        /// Encrypts <paramref name="plaintext"/> under <paramref name="tenantId"/>'s derived key.
        /// </summary>
        /// <param name="tenantId">Tenant the file is being issued to; selects the derived key.</param>
        /// <param name="plaintext">
        /// File content as text. Encoded UTF-8 without a byte-order mark — see
        /// <c>BatchFileFormat</c> for why the BOM matters.
        /// </param>
        /// <returns>
        /// Ciphertext laid out as [12-byte nonce][16-byte GCM tag][ciphertext], byte-for-byte
        /// what <see cref="IBatchFileDecryptor.Decrypt"/> expects. A fresh cryptographically
        /// random nonce is generated per call — never a counter, never derived from the tenant id,
        /// because nonce reuse under a fixed GCM key leaks both the plaintext XOR and the
        /// authentication subkey. Failure is configuration-level only
        /// (<c>BatchErrors.EncryptionFailed()</c>), never caller-driven.
        /// </returns>
        Result<byte[]> Encrypt(long tenantId, string plaintext);
    }
}
