using System;
using System.Security.Cryptography;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// Decrypt-only counterpart to the Matica Printer Agent's own
    /// <c>AesGcmFileEncryptionService</c> — same format, same algorithm, same field order,
    /// deliberately not reimplemented independently. Format:
    /// <code>v1.{base64 nonce}.{base64 tag}.{base64 ciphertext}</code>
    /// AES-256-GCM, 12-byte nonce, 16-byte authentication tag — matching the Printer Agent's own
    /// constants exactly. No <c>Encrypt</c> method: the Inventory API never writes a Printer
    /// Agent file, it only ever reads and decrypts one that the Printer Agent already wrote, on a
    /// Super Admin's behalf — an encrypt method here would be dead code with no real caller.
    /// </summary>
    public sealed class AesGcmPrintAgentFileDecryptionService : IPrintAgentFileDecryptionService
    {
        private const string VersionMarker = "v1";
        private const int TagSizeBytes = 16;

        private readonly byte[] _key;

        /// <summary>Creates the service from its configured key.</summary>
        public AesGcmPrintAgentFileDecryptionService(IOptions<FileEncryptionOptions> options)
        {
            _key = Convert.FromBase64String(options.Value.Key);
        }

        /// <inheritdoc />
        public string Decrypt(string encoded)
        {
            string[] segments = encoded.Split('.');
            if (segments.Length != 4 || segments[0] != VersionMarker)
            {
                throw new PrintAgentFileFormatException(
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
                throw new PrintAgentFileFormatException("One or more segments are not valid base64.", ex);
            }

            byte[] plaintextBytes = new byte[ciphertext.Length];
            using (var aesGcm = new AesGcm(_key, TagSizeBytes))
            {
                // Throws CryptographicException on tag mismatch - tampered or corrupted data.
                // Deliberately not caught here, same reasoning as the Printer Agent's own service:
                // PrintAgentFileFormatException (wrong shape) and CryptographicException (right
                // shape, failed the tag check) are different failure categories the Super Admin
                // endpoint needs to report differently.
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
