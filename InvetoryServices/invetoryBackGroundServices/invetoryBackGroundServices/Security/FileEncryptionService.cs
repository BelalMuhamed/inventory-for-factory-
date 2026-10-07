using System;
using System.Security.Cryptography;
using System.Text;
using invetoryBackGroundServices.Options;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Security
{
    /// <summary>
    /// Shared AES-GCM encryption for anything this service persists to disk that needs
    /// confidentiality - log lines and Outbox files alike, one implementation rather than one per
    /// caller. Deliberately a plain <c>string → encoded string</c> / <c>encoded string → string</c>
    /// contract: it has no opinion about whether the caller's unit of encryption is one log line
    /// or one whole file, that's the caller's decision, not this service's.
    /// </summary>
    public interface IFileEncryptionService
    {
        /// <summary>Encrypts <paramref name="plaintext"/>, returning a self-contained encoded string.</summary>
        string Encrypt(string plaintext);

        /// <summary>
        /// Decrypts a string produced by <see cref="Encrypt"/>.
        /// </summary>
        /// <exception cref="FileEncryptionFormatException">
        /// <paramref name="encoded"/> is not in this service's encoded format at all (wrong
        /// version marker, wrong number of segments, invalid base64) - the caller-facing signal
        /// that this string was never produced by <see cref="Encrypt"/> in the first place, as
        /// opposed to being produced by it and then corrupted/tampered with.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// <paramref name="encoded"/> has the right shape but fails AES-GCM's authentication tag
        /// check - a tampered or corrupted ciphertext, not a format problem.
        /// </exception>
        string Decrypt(string encoded);

        /// <summary>
        /// True if <paramref name="value"/> looks like this service's encoded format (starts with
        /// the current version marker and has the right segment count) - a cheap, non-throwing
        /// check callers use to distinguish "this is one of our encrypted values" from "this is
        /// something else entirely" (plaintext JSON, an old TripleDES-encrypted line) before
        /// deciding whether to call <see cref="Decrypt"/> at all. Does not validate the
        /// authentication tag - a value can look like the right format and still fail
        /// <see cref="Decrypt"/> if it's been tampered with.
        /// </summary>
        bool LooksEncrypted(string value);
    }

    /// <summary><paramref name="encoded"/> is not in <see cref="IFileEncryptionService"/>'s encoded format at all.</summary>
    public sealed class FileEncryptionFormatException : Exception
    {
        public FileEncryptionFormatException(string message) : base(message) { }
        public FileEncryptionFormatException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// AES-256-GCM implementation. Format, deliberately explicit rather than relying on
    /// undocumented byte offsets:
    /// <code>v1.{base64 nonce}.{base64 tag}.{base64 ciphertext}</code>
    /// A dot-separated, versioned, per-field-base64 string - readable as four distinct segments
    /// by inspection, not a single opaque blob, and the leading version segment is what makes a
    /// future format change (or, just as importantly here, detecting content that was never in
    /// this format at all - a plaintext JSON file, an old TripleDES-encrypted log line) a matter
    /// of checking a prefix rather than guessing.
    /// <para>
    /// Nonce: 12 bytes (96 bits), freshly generated with <see cref="RandomNumberGenerator"/> on
    /// every single call to <see cref="Encrypt"/> - never reused, never derived from anything
    /// static. This is the specific defect being fixed: the previous TripleDES implementation used
    /// a fixed, all-zero IV for every encryption, which meant identical plaintext always produced
    /// identical ciphertext - confirmed directly against real log files, where the same ciphertext
    /// block appeared verbatim across files over a year apart. A fresh random nonce per call is
    /// what actually closes that specific gap, not just switching algorithms.
    /// </para>
    /// <para>
    /// Tag: 16 bytes (128 bits), AES-GCM's standard authentication tag - verified automatically by
    /// <see cref="AesGcm.Decrypt"/>, which throws <see cref="CryptographicException"/> rather than
    /// returning corrupted plaintext if the ciphertext or tag has been altered. This is what
    /// "tampering is detected, decryption fails safely" actually means here - it's not bolted on,
    /// it's inherent to AES-GCM being an authenticated cipher.
    /// </para>
    /// </summary>
    public sealed class AesGcmFileEncryptionService : IFileEncryptionService
    {
        private const string VersionMarker = "v1";
        private const int NonceSizeBytes = 12;
        private const int TagSizeBytes = 16;

        private readonly byte[] _key;

        /// <summary>Creates the service from its configured key.</summary>
        public AesGcmFileEncryptionService(IOptions<FileEncryptionOptions> options)
        {
            _key = Convert.FromBase64String(options.Value.Key);
        }

        /// <inheritdoc />
        public string Encrypt(string plaintext)
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (var aesGcm = new AesGcm(_key, TagSizeBytes))
            {
                aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            }

            return string.Join('.',
                VersionMarker,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(ciphertext));
        }

        /// <inheritdoc />
        public string Decrypt(string encoded)
        {
            string[] segments = encoded.Split('.');
            if (segments.Length != 4 || segments[0] != VersionMarker)
            {
                throw new FileEncryptionFormatException(
                    $"Value is not in the expected '{VersionMarker}.<nonce>.<tag>.<ciphertext>' format.");
            }

            byte[] nonce, tag, ciphertext;
            try
            {
                nonce = Convert.FromBase64String(segments[1]);
                tag = Convert.FromBase64String(segments[2]);
                ciphertext = Convert.FromBase64String(segments[3]);
            }
            catch (FormatException ex)
            {
                throw new FileEncryptionFormatException("One or more segments are not valid base64.", ex);
            }

            byte[] plaintextBytes = new byte[ciphertext.Length];
            using (var aesGcm = new AesGcm(_key, TagSizeBytes))
            {
                // Throws CryptographicException on tag mismatch - tampered or corrupted data.
                // Deliberately not caught here: FileEncryptionFormatException and
                // CryptographicException are different failure categories on purpose (wrong
                // shape entirely, vs. right shape but the content doesn't check out), and callers
                // that care about the distinction (the reconciliation job's corrupt-file handling,
                // the future Super Admin endpoint) need to be able to tell them apart.
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);
            }

            return Encoding.UTF8.GetString(plaintextBytes);
        }

        /// <inheritdoc />
        public bool LooksEncrypted(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] segments = value.Split('.');
            return segments.Length == 4 && segments[0] == VersionMarker;
        }
    }
}
