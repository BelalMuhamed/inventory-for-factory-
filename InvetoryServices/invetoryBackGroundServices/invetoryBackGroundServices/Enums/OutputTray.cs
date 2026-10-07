namespace invetoryBackGroundServices.Enums
{
    /// <summary>
    /// Mirrors the Inventory API's <c>DomainLayer.Enums.OutputTray</c> values exactly — shared by
    /// both <c>EvolisPrintReqDto.OutputTray</c> and <c>EvolisPrintReqDto.ErrorTray</c>, same as the
    /// Inventory API's own entity. Mapped by <c>Machine.Evolis.EvolisPrintSettingsMapper</c> to the
    /// real SDK enum of the identical name — confirmed by live reflection
    /// (<c>Connection.SetOutputTray(Evolis.OutputTray)</c> / <c>SetErrorTray(Evolis.OutputTray)</c>).
    /// </summary>
    public enum OutputTray : short
    {
        STANDARD = 1,
        STANDARDSTANDBY = 2,
        MANUAL = 4,
        ERROR = 8,
        ERRORSTANDBY = 16,
        EJECT = 32,
        BEZEL = 64,
        ERRORSLOT = 128,
        LOCKED = 256
    }
}
