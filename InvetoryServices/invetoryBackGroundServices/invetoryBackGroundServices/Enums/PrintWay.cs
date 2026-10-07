namespace invetoryBackGroundServices.Enums
{
    /// <summary>
    /// Card orientation for Evolis printing (Evolis Integration Plan §9, decision Q-06). Mirrors
    /// the Inventory API's <c>DomainLayer.Enums.PrintWay</c> values exactly — a wire contract
    /// shared between two independently-deployed services, kept as a plain local enum here
    /// rather than a shared reference, same reasoning as <see cref="UsingPrinterType"/>.
    /// </summary>
    public enum PrintWay : byte
    {
        /// <summary>Card printed in landscape orientation. Matches the Inventory API's value 0.</summary>
        Landscape = 0,

        /// <summary>Card printed in portrait orientation. Matches the Inventory API's value 1.</summary>
        Portrait = 1
    }
}
