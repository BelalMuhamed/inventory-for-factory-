namespace invetoryBackGroundServices.Enums
{
    /// <summary>
    /// Printer family, matching the Inventory API's <c>DomainLayer.Enums.UsingPrinterType</c>
    /// exactly (a wire/keyed-service contract shared between two independently-deployed
    /// services — kept as a plain local enum here rather than a shared reference, since the two
    /// projects don't share an assembly). Backs the keyed DI registration of
    /// <see cref="Machine.Abstractions.ICardPrinterAdapter"/> implementations (Evolis Integration
    /// Plan §8, decision Q-17).
    /// </summary>
    public enum UsingPrinterType : byte
    {
        /// <summary>Evolis printer family. Matches the Inventory API's value 0.</summary>
        Evolis = 0,

        /// <summary>Matica printer family. Matches the Inventory API's value 1.</summary>
        Matica = 1
    }
}
