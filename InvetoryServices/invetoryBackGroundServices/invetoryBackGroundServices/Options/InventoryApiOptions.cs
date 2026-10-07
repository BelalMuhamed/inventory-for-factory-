namespace invetoryBackGroundServices.Options
{
    /// <summary>
    /// Strongly-typed settings for reaching the Inventory API, bound from the
    /// <c>"InventoryApi"</c> configuration section. Replaces the old ad-hoc
    /// <c>Configuration["WebAPI"]</c> string-indexer pattern (<c>cls/API_Handle.cs</c>, now
    /// removed) with the Options pattern, matching how the rest of this codebase's sibling
    /// project (the Inventory API itself) configures everything else.
    /// </summary>
    public sealed class InventoryApiOptions
    {
        /// <summary>Configuration section name these options bind from.</summary>
        public const string SectionName = "InventoryApi";

        /// <summary>
        /// Base URL of the Inventory API, including a trailing slash (e.g.
        /// <c>https://localhost:7193/</c>). Backend Call #1/#2 are issued against
        /// <c>{BaseUrl}api/print-flow/...</c>.
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;
    }
}
