namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for card-file generation, bound from the <c>"CardFile"</c> section
    /// (Card File Generation, Phase 9.5).
    /// </summary>
    public sealed class CardFileOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "CardFile";

        /// <summary>
        /// Upper bound on cards per request. Without a cap, one request builds an unbounded
        /// plaintext string plus an unbounded base64 response in memory — a trivial way to
        /// exhaust the server. Defaulted rather than required so the endpoint is safe with no
        /// configuration at all.
        /// </summary>
        public int MaxCardsPerRequest { get; set; } = 50_000;
    }
}
