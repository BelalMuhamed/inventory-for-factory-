namespace invetoryBackGroundServices.Enums
{
    /// <summary>
    /// Which physical face of the card a print element applies to (Evolis Integration Plan §9).
    /// Mirrors the Inventory API's <c>DomainLayer.Enums.PrintedFace</c> values exactly — same
    /// wire-contract reasoning as <see cref="PrintWay"/>.
    /// </summary>
    public enum PrintedFace : byte
    {
        /// <summary>Front of the card. Matches the Inventory API's value 0.</summary>
        Front = 0,

        /// <summary>Back of the card. Matches the Inventory API's value 1.</summary>
        Back = 1
    }
}
