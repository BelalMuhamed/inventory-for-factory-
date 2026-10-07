namespace invetoryBackGroundServices.Enums
{
    /// <summary>
    /// Mirrors the Inventory API's <c>DomainLayer.Enums.MagCoercivity</c> values exactly. Mapped by
    /// <c>Machine.Evolis.EvolisPrintSettingsMapper</c> to the real SDK enum of the identical name,
    /// passed directly to <c>MagSession.SetCoercivity(Evolis.MagCoercivity)</c> — confirmed by live
    /// reflection against the installed Evolis.SDK 9.4.1 (not a <c>SetSetting</c> string call).
    /// </summary>
    public enum MagCoercivity : byte
    {
        PRINTER = 0,
        AUTO = 1,
        LOCO = 2,
        HICO = 3
    }
}
