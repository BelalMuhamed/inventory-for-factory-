namespace invetoryBackGroundServices.Enums
{
    /// <summary>
    /// Mirrors the Inventory API's <c>DomainLayer.Enums.InputTray</c> values exactly — a wire
    /// contract shared between two independently-deployed services, same reasoning as
    /// <see cref="PrintWay"/>. Carried on <c>Params.EvolisPrintReqDto</c> (sourced from the
    /// printer's own <c>EvolisPrinterConfiguration</c> row, which Angular already has from
    /// <c>GET /api/printers/{id}</c>) and mapped by <c>Machine.Evolis.EvolisPrintSettingsMapper</c>
    /// to the real SDK enum of the identical name — confirmed by live reflection against the
    /// installed <c>Evolis.SDK</c> 9.4.1 (<c>Connection.SetInputTray(Evolis.InputTray)</c>, not the
    /// unrelated <c>SettingsKey.GInputTray</c> string vocabulary — see CLAUDE.md's Phase 5 section
    /// for why the two are not interchangeable).
    /// </summary>
    public enum InputTray : short
    {
        FEEDER = 1,
        MANUAL = 2,
        MANUALANDCMD = 4,
        BEZEL = 8,
        BOTH = 16,
        NOFEEDER = 32,
        REAR = 64
    }
}
