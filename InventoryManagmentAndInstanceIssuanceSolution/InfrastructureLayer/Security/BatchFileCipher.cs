using System;
using System.Security.Cryptography;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using DomainLayer.Common;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// AES-256-GCM implementation of the batch-file cipher (Batch Upload Phased Plan, Phase 2;
    /// encryption added in Card File Generation, Phase 9.3). Derives a distinct key per tenant
    /// from the configured master secret via PBKDF2 — the derived key is computed on demand and
    /// never stored. Ciphertext layout: [12-byte nonce][16-byte GCM tag][ciphertext].
    /// <para>
    /// One class implements both <see cref="IBatchFileEncryptor"/> and
    /// <see cref="IBatchFileDecryptor"/> specifically so that <see cref="DeriveTenantKey"/> has
    /// exactly one definition. A second derivation routine written independently for the
    /// generation side would be a round-trip failure waiting to happen.
    /// </para>
    /// </summary>
    public sealed class BatchFileCipher : IBatchFileEncryptor, IBatchFileDecryptor
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;       // AES-256
        private const int Pbkdf2Iterations = 100_000;

        private readonly BatchCipherOptions _options;

        /// <summary>Creates the cipher bound to the configured master secret/salt.</summary>
        /// <param name="options">Batch-cipher settings (user-secrets/environment-backed).</param>
        public BatchFileCipher(IOptions<BatchCipherOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public Result<byte[]> Encrypt(long tenantId, string plaintext)
        {
            try
            {
                byte[] key = DeriveTenantKey(tenantId);

                // Encoding.UTF8.GetBytes never emits a preamble, so the file is BOM-free by
                // construction. Building the plaintext through a StreamWriter would not be —
                // see BatchFileFormat's remarks on why a stray BOM breaks every row-1 PAN.
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);

                byte[] output = new byte[NonceSize + TagSize + plaintextBytes.Length];

                Span<byte> nonce = output.AsSpan(0, NonceSize);
                Span<byte> tag = output.AsSpan(NonceSize, TagSize);
                Span<byte> cipher = output.AsSpan(NonceSize + TagSize);

                // Fresh random nonce per file. GCM nonce reuse under a fixed key is catastrophic:
                // it discloses the XOR of the two plaintexts and, worse, the authentication
                // subkey, which makes tag forgery possible. 96 random bits puts the collision
                // bound near 2^32 files per tenant — far outside any realistic issuance volume.
                RandomNumberGenerator.Fill(nonce);

                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Encrypt(nonce, plaintextBytes, cipher, tag);
                }

                return Result.Success(output);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
            {
                // Not caller-driven: reaching here means the configured master secret or salt is
                // unusable (e.g. a non-base64 salt). Surfaced as an opaque internal failure so no
                // key material detail leaks into the response.
                return Result.Failure<byte[]>(BatchErrors.EncryptionFailed());
            }
        }

        /// <inheritdoc />
        public Result<string> Decrypt(long tenantId, byte[] ciphertext)
        {
            if (ciphertext is null || ciphertext.Length < NonceSize + TagSize)
            {
                return Result.Failure<string>(BatchErrors.DecryptionFailed());
            }

            try
            {
                byte[] key = DeriveTenantKey(tenantId);

                ReadOnlySpan<byte> input = ciphertext;
                ReadOnlySpan<byte> nonce = input[..NonceSize];
                ReadOnlySpan<byte> tag = input.Slice(NonceSize, TagSize);
                ReadOnlySpan<byte> cipher = input[(NonceSize + TagSize)..];

                byte[] plaintext = new byte[cipher.Length];

                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Decrypt(nonce, cipher, tag, plaintext);
                }

                return Result.Success(Encoding.UTF8.GetString(plaintext));
            }
            catch (CryptographicException)
            {
                // Wrong key or tampered/corrupted ciphertext — GCM tag check failed. Expected
                // failure mode per the plan: a clean Result, not a bubbled exception.
                return Result.Failure<string>(BatchErrors.DecryptionFailed());
            }
        }

        // Combines the configured salt with the tenant id so the same master secret yields a
        // distinct key per tenant. Computed fresh on every call — never cached, never persisted.
        // Shared by both directions; changing it invalidates every previously issued file.
        private byte[] DeriveTenantKey(long tenantId)
        {
            byte[] configuredSalt = Convert.FromBase64String(_options.Salt);
            byte[] tenantSalt = new byte[configuredSalt.Length + sizeof(long)];
            Buffer.BlockCopy(configuredSalt, 0, tenantSalt, 0, configuredSalt.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(tenantId), 0, tenantSalt, configuredSalt.Length, sizeof(long));

            using var kdf = new Rfc2898DeriveBytes(_options.MasterSecret, tenantSalt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(KeySize);
        }
    }
}
