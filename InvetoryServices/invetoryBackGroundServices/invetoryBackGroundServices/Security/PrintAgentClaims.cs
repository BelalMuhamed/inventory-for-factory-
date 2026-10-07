namespace invetoryBackGroundServices.Security
{
    /// <summary>
    /// Claim names carried by the Print Agent token, matching the Inventory API's
    /// <c>PrintAgentTokenGenerator</c> constants exactly (a wire contract shared between two
    /// independently-deployed services — kept as plain string constants here rather than a shared
    /// reference, since the two projects don't share an assembly).
    /// </summary>
    public static class PrintAgentClaims
    {
        /// <summary>Claim carrying the tenant id the token is scoped to.</summary>
        public const string TenantId = "tenantId";

        /// <summary>Claim carrying the branch id the token is scoped to.</summary>
        public const string BranchId = "branchId";

        /// <summary>Claim carrying the printer id the token is scoped to.</summary>
        public const string PrinterId = "printerId";

        /// <summary>Fixed claim identifying this token as a Print Agent token.</summary>
        public const string Purpose = "purpose";

        /// <summary>The only valid <see cref="Purpose"/> value.</summary>
        public const string PurposeValue = "print-agent";
    }
}
