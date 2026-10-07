namespace invetoryBackGroundServices.Security
{
    /// <summary>
    /// Central definition of this service's one authorization policy, matching the Inventory
    /// API's own <c>AuthorizationPolicies</c> convention.
    /// </summary>
    public static class PrintAgentAuthPolicy
    {
        /// <summary>Policy name requiring a valid Print Agent token.</summary>
        public const string Name = "PrintAgentOnly";
    }
}
