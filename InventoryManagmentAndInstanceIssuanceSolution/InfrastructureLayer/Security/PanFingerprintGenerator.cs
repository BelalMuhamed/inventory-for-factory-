using System;
using System.Security.Cryptography;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// HMAC-SHA256 implementation of the PAN fingerprint generator (PAN Storage Redesign).
    /// Derives a distinct key per tenant from the configured master secret via PBKDF2 — same
    /// derivation shape as <see cref="BatchFileCipher"/>, but with an independent secret
    /// (<see cref="PanHashOptions"/>): the file-decryption key and the PAN-identity key must
    /// never be linkable to each other.
    /// <para>
    /// A plain (unkeyed) hash of a PAN is brute-forceable offline: the BIN is public and the
    /// last digit is a Luhn check digit, leaving a small enough keyspace per BIN to defeat a
    /// salted-but-keyless hash in practice. Only a secret the attacker does not have — the HMAC
    /// key — blocks that. This is why <c>Fingerprint</c> is keyed, not a bare
    /// <c>SHA256.HashData</c> call.
    /// </para>
    /// <para>
    /// Registered <c>Scoped</c> (one instance per HTTP request / one batch upload). The derived
    /// key is cached on the instance after the first call: <c>Fingerprint</c> runs once per row
    /// in a batch upload, and re-deriving via 100,000 PBKDF2 iterations on every row would turn a
    /// several-thousand-row batch into a multi-second-per-request cost. Caching per scoped
    /// instance needs no manual invalidation — it dies with the request.
    /// </para>
    /// </summary>
    public sealed class PanFingerprintGenerator : IPanFingerprintGenerator
    {
        private const int KeySize = 32;       // HMAC-SHA256 key/output size
        private const int Pbkdf2Iterations = 100_000;

        private readonly PanHashOptions _options;

        private long? _cachedTenantId;
        private byte[]? _cachedKey;

        /// <summary>Creates the generator bound to the configured master secret/salt.</summary>
        /// <param name="options">PAN-hash settings (user-secrets/environment-backed).</param>
        public PanFingerprintGenerator(IOptions<PanHashOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public byte[] Fingerprint(long tenantId, string normalizedPan)
        {
            byte[] key = GetOrDeriveKey(tenantId);
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedPan ?? string.Empty));
        }

        // Combines the configured salt with the tenant id so the same master secret yields a
        // distinct key per tenant — same pattern as BatchFileCipher.DeriveTenantKey, deliberately
        // duplicated rather than shared: these two derivations must be able to evolve
        // independently (key separation), so a shared helper would be the wrong coupling.
        private byte[] GetOrDeriveKey(long tenantId)
        {
            if (_cachedKey is not null && _cachedTenantId == tenantId)
            {
                return _cachedKey;
            }

            byte[] configuredSalt = Convert.FromBase64String(_options.Salt);
            byte[] tenantSalt = new byte[configuredSalt.Length + sizeof(long)];
            Buffer.BlockCopy(configuredSalt, 0, tenantSalt, 0, configuredSalt.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(tenantId), 0, tenantSalt, configuredSalt.Length, sizeof(long));

            using var kdf = new Rfc2898DeriveBytes(_options.MasterSecret, tenantSalt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            byte[] key = kdf.GetBytes(KeySize);

            _cachedTenantId = tenantId;
            _cachedKey = key;
            return key;
        }
    }
}
