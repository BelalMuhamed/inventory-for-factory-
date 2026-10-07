namespace ApplicationLayer.Security
{
    /// <summary>
    /// Stateless PAN masking (Batch Upload Phased Plan §Q1 / Phase 2). Always computed from the
    /// plaintext PAN, independently of card identity (see <c>IPanFingerprintGenerator</c>).
    /// No key, no options: pure functions, safe to call from anywhere without DI.
    /// </summary>
    public static class PanMasker
    {
        private const int VisibleDigits = 6;
        private const string MaskPrefix = "**********"; // ten mask characters (Q1)

        /// <summary>
        /// Masks <paramref name="pan"/> as ten mask characters followed by its last six digits.
        /// Safe to call on malformed/short input (batch rows may fail PAN validation but still
        /// need "mask what is present" per the Phase 6 per-row rules) — never throws.
        /// </summary>
        public static string Mask(string? pan) => MaskPrefix + Last6(pan);

        /// <summary>
        /// Returns the last six characters of <paramref name="pan"/>, or the whole string if it
        /// has six characters or fewer, or an empty string if null/empty. Never throws.
        /// </summary>
        public static string Last6(string? pan)
        {
            if (string.IsNullOrEmpty(pan))
            {
                return string.Empty;
            }

            return pan.Length <= VisibleDigits ? pan : pan[^VisibleDigits..];
        }
    }
}
