using System.Collections.Generic;
using invetoryBackGroundServices.Machine.Abstractions;
using invetoryBackGroundServices.Machine.Rendering;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Translates <see cref="CardJob"/> + a rendered <see cref="CardRenderResult"/> into a
    /// vendor-neutral <see cref="CardPrintRequest"/> (Evolis Integration Plan §10, step 8).
    /// <para>
    /// Real SDK finding, live-verified against the connected Evolis Primacy 2 while building this
    /// phase (see CLAUDE.md's Phase 5 section) — not in the plan, which assumed a single
    /// vocabulary: <c>PrintSession.SetSetting(SettingsKey.GRibbonType, ...)</c> does NOT accept
    /// the real <c>Evolis.RibbonType</c> C# enum member names (<c>RibbonTypes.SdkCode</c> as
    /// seeded in Phase 1, e.g. <c>"KWHITE"</c>) — it has its own separate string vocabulary,
    /// confirmed from the installed SDK's own XML docs
    /// (<c>F:Evolis.SettingsKey.GRibbonType</c>'s "Possible values" list) and live
    /// <c>SetSetting</c> round-trips: colour ribbons get an <c>RC_</c> prefix, monochrome/specialty
    /// ribbons get an <c>RM_</c> prefix. <see cref="RibbonSdkCodeToSettingsToken"/> is the explicit,
    /// verified lookup for exactly the 10 ribbons Phase 1 seeded — not a guessed prefix rule.
    /// <c>CLEAR</c> (<c>Evolis.RibbonType.CLEAR</c>, confirmed real and reported live by
    /// <c>Connection.GetRibbonInfo()</c>) has <strong>no</strong> corresponding
    /// <c>GRibbonType</c> settings token at all — confirmed live by trying <c>"CLEAR"</c>,
    /// <c>"RM_CLEAR"</c>, and <c>"RC_CLEAR"</c>, all rejected. A product configured with the
    /// <c>CLEAR</c> ribbon cannot be printed through <c>PrintSession.SetSetting</c> on this SDK
    /// version; <see cref="TryMapRibbon"/> returns false for it so the orchestrator can fail the
    /// job with a clear, actionable error instead of a silent no-op <c>SetSetting</c> failure.
    /// </para>
    /// <para>
    /// Second real finding: trays are set via the typed <c>Connection.SetInputTray</c>/
    /// <c>SetOutputTray</c>/<c>SetErrorTray</c> calls (the same <c>Evolis.InputTray</c>/
    /// <c>OutputTray</c> enums already used by <c>EvolisPrinterConfiguration</c>), confirmed live
    /// — NOT <c>SetSetting(GInputTray/GOutputTray, ...)</c>, whose own "Possible values" vocabulary
    /// (<c>PRINTER, FEEDER, AUTO, MANUAL, HOPPER, BEZEL</c> for input;
    /// <c>PRINTER, HOPPER, REAR, MANUAL, REJECT, BEZEL</c> for output) does not even cover every
    /// value the <c>InputTray</c>/<c>OutputTray</c> enums have (e.g. <c>STANDARD</c>, this
    /// deployment's own configured default output tray, has no <c>GOutputTray</c> token at all —
    /// confirmed live, every attempt returned <c>false</c>). The plan's own step-8 pseudocode
    /// (<c>SetSetting(GOutputTray, config.OutputTray)</c>) is incompatible with this SDK version
    /// as literally written; <see cref="EvolisPrinterConnection"/> calls the typed Connection-level
    /// setters instead.
    /// </para>
    /// </summary>
    public sealed class EvolisPrintSettingsMapper
    {
        /// <summary>
        /// Exact, live-verified mapping from <c>RibbonTypes.SdkCode</c> (the real
        /// <c>Evolis.RibbonType</c> enum member name) to the token
        /// <c>PrintSession.SetSetting(SettingsKey.GRibbonType, ...)</c> actually accepts. Limited
        /// to the 10 ribbons Phase 1 seeded — add an entry here (and verify it live) before
        /// seeding any additional ribbon.
        /// </summary>
        private static readonly Dictionary<string, string> RibbonSdkCodeToSettingsToken = new()
        {
            ["YMCKO"] = "RC_YMCKO",
            ["YMCK"] = "RC_YMCK",
            ["YMCKOK"] = "RC_YMCKOK",
            ["KBLACK"] = "RM_KBLACK",
            ["KWHITE"] = "RM_KWHITE",
            ["KRED"] = "RM_KRED",
            ["KBLUE"] = "RM_KBLUE",
            ["KGREEN"] = "RM_KGREEN",
            ["HOLO"] = "RM_HOLO",
            // CLEAR deliberately absent - no SettingsKey.GRibbonType token exists for it (see
            // this class's remarks). TryMapRibbon returns false, not a wrong/guessed token.
        };

        /// <summary>
        /// Maps a <c>RibbonTypes.SdkCode</c> value to its <c>GRibbonType</c> settings token.
        /// False (with <paramref name="settingsToken"/> null) when no mapping exists — the
        /// ribbon is real but not selectable via this SDK call (currently only <c>CLEAR</c>).
        /// </summary>
        public bool TryMapRibbon(string ribbonSdkCode, out string? settingsToken) =>
            RibbonSdkCodeToSettingsToken.TryGetValue(ribbonSdkCode, out settingsToken!);

        /// <summary>Builds the vendor-neutral print request for step 8 from the job and its rendered bitmap.</summary>
        public CardPrintRequest ToPrintRequest(CardJob job, CardRenderResult renderResult, string ribbonSettingsToken) =>
            new(renderResult.BmpBytes, renderResult.Panel, job.PrintedFace, ribbonSettingsToken);
    }
}
