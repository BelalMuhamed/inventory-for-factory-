using System;
using System.Linq;

namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// The single source of truth for the batch card-file wire format (Card File Generation,
    /// Phase 9.1). Both sides of the contract depend on this type and nothing else:
    /// <see cref="BatchRowParser"/> consumes files described here, and
    /// <see cref="ApplicationLayer.CardFiles.CardFileWriter"/> produces them.
    /// <para>
    /// Before this type existed the delimiter, field count, and PAN rules were <c>private const</c>
    /// inside the parser. Duplicating them in the writer would let the two sides drift silently:
    /// the server would happily emit a file that every tenant then rejects row-by-row, with no
    /// server-side signal that anything is wrong. Changing the format now means changing it here,
    /// once.
    /// </para>
    /// <para>
    /// Format: one card per line, <c>PAN|ProductName|BranchName</c>, UTF-8 with no byte-order mark.
    /// The BOM exclusion is load-bearing — <c>BatchFileCipher.Decrypt</c> finishes with
    /// <c>Encoding.UTF8.GetString</c>, which does not strip a preamble, so a BOM would survive
    /// decryption and prepend U+FEFF to the first PAN, failing every row-1 digit check.
    /// </para>
    /// </summary>
    public static class BatchFileFormat
    {
        /// <summary>Separates the three fields within a row.</summary>
        public const char FieldDelimiter = '|';

        /// <summary>Fields required per row: PAN, product name, branch name — in that order.</summary>
        public const int RequiredFieldCount = 3;

        /// <summary>Shortest accepted PAN length.</summary>
        public const int MinPanLength = 13;

        /// <summary>Longest accepted PAN length.</summary>
        public const int MaxPanLength = 19;

        /// <summary>
        /// Line separator emitted by the writer. The parser accepts <c>\r\n</c> as well
        /// (see <see cref="SplitLines"/>) so hand-edited files still import.
        /// </summary>
        public const string LineSeparator = "\n";

        /// <summary>
        /// The only file extension the upload endpoint accepts. Note this is a contract guard,
        /// not a security control — the actual integrity check is the AES-GCM authentication tag.
        /// </summary>
        public const string FileExtension = ".dat";

        private static readonly string[] LineSeparatorCandidates = { "\r\n", "\n" };

        /// <summary>
        /// Splits decrypted file content into rows, tolerating either line ending.
        /// Empty entries are preserved: a blank line in the middle of a file is a real
        /// (malformed) row and must still be reported, not silently dropped.
        /// </summary>
        /// <param name="fileContent">Decrypted plaintext file content.</param>
        public static string[] SplitLines(string fileContent) =>
            fileContent.Split(LineSeparatorCandidates, StringSplitOptions.None);

        /// <summary>
        /// Canonicalizes a PAN for validation and comparison: trims surrounding whitespace and
        /// removes the interior spaces commonly present in copy-pasted card numbers. Never throws.
        /// </summary>
        /// <param name="pan">Raw PAN as supplied by the caller or read from a file row.</param>
        public static string NormalizePan(string? pan) =>
            pan is null ? string.Empty : pan.Trim().Replace(" ", string.Empty);

        /// <summary>
        /// True when <paramref name="pan"/> is 13–19 digits and satisfies the Luhn checksum.
        /// Expects an already-normalized value (see <see cref="NormalizePan"/>).
        /// </summary>
        /// <param name="pan">Normalized PAN.</param>
        public static bool IsValidPan(string? pan)
        {
            if (string.IsNullOrEmpty(pan) || pan.Length is < MinPanLength or > MaxPanLength)
            {
                return false;
            }

            return pan.All(char.IsDigit);
        }

        /// <summary>
        /// True when <paramref name="value"/> contains a character that would corrupt the row
        /// structure: the field delimiter, or either line-ending character. A product named
        /// <c>"Visa|Gold"</c> would otherwise produce a four-field row that the parser rejects as
        /// malformed at the tenant's end, long after the generating request succeeded.
        /// </summary>
        /// <param name="value">A product or branch name destined for a file row.</param>
        public static bool ContainsForbiddenCharacter(string? value) =>
            value is not null &&
            (value.Contains(FieldDelimiter) || value.Contains('\r') || value.Contains('\n'));

        /// <summary>
        /// Standard Luhn (mod-10) checksum: from the rightmost digit, double every second digit;
        /// if doubling pushes a digit above 9, subtract 9 (equivalent to summing its two digits).
        /// Valid when the total is a multiple of 10.
        /// </summary>
        /// <param name="digits">A string known to contain only decimal digits.</param>
        //public static bool PassesLuhnCheck(string digits)
        //{
        //    int sum = 0;
        //    bool doubleDigit = false;

        //    for (int i = digits.Length - 1; i >= 0; i--)
        //    {
        //        int digit = digits[i] - '0';

        //        if (doubleDigit)
        //        {
        //            digit *= 2;
        //            if (digit > 9)
        //            {
        //                digit -= 9;
        //            }
        //        }

        //        sum += digit;
        //        doubleDigit = !doubleDigit;
        //    }

        //    return sum % 10 == 0;
        //}
    }
}
