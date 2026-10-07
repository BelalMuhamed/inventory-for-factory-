using invetoryBackGroundServices.Enums;

namespace invetoryBackGroundServices.Params
{
    /// <summary>
    /// Request body for <c>POST api/Machine/Print-Card-Holder-Name-Evolis</c> (Evolis Integration
    /// Plan §10, decisions Q-26/Q-27) — Evolis-shaped, deliberately separate from
    /// <see cref="PrintReqDto"/> (Matica), which stays untouched. No <c>MachineIp</c>/<c>Port</c>/
    /// <c>FeederId</c>/<c>HopperId</c>/<c>Font</c>/<c>Cpi</c>/<c>OffsetX</c>/<c>OffsetY</c>/tipper
    /// fields — none apply: Evolis is addressed by <see cref="DeviceName"/> (the SDK connection
    /// key), and layout comes from the fields below, sourced the same way Matica's own layout
    /// fields are — Angular fetches them from <c>GET /api/products/{id}/print-config</c> as part
    /// of normal product browsing and passes them through here unchanged.
    /// <para>
    /// Real gap found and closed while building Phase 5 (see CLAUDE.md): the plan said trays come
    /// from <c>EvolisPrinterConfiguration</c> "not the request," but never specified how the Agent
    /// — which has no database and no session with the Inventory API beyond the forwarded Print
    /// Agent token — would obtain that printer-level config out of band. Since the plan's own
    /// "Backend/API: None" constraint rules out a new fetch call from the Agent itself, the tray
    /// and coercivity fields are caller-supplied here instead, the same way <see cref="RibbonSdkCode"/>
    /// already is: Angular already has them in hand from <c>GET /api/printers/{id}</c> (Phase 1)
    /// when the operator selects which Evolis printer to print on, so it passes them through.
    /// </para>
    /// </summary>
    public sealed class EvolisPrintReqDto
    {
        public required string CardHolderName { get; set; }

        /// <summary>The SDK connection key (<c>Evolis.Device.name</c>) — identifies which physical printer to use.</summary>
        public required string DeviceName { get; set; }

        public required long ProductId { get; set; }
        public required long BranchId { get; set; }

        /// <summary>The product's configured ribbon (FK id, for reference/logging — <see cref="RibbonSdkCode"/> is what the compatibility check actually uses).</summary>
        public required long RibbonTypeId { get; set; }

        /// <summary>The product's configured ribbon, as the exact <c>Evolis.RibbonType</c> C# enum member name (e.g. <c>"KWHITE"</c>, <c>"YMCKO"</c>) — <c>RibbonTypes.SdkCode</c> verbatim, NOT the <c>SettingsKey.GRibbonType</c> string token. <c>EvolisPrintSettingsMapper</c> translates between the two — see CLAUDE.md's Phase 5 section.</summary>
        public required string RibbonSdkCode { get; set; }

        /// <summary>From the printer's own <c>EvolisPrinterConfiguration.InputTray</c> — see this class's remarks.</summary>
        public required InputTray InputTray { get; set; }

        /// <summary>From <c>EvolisPrinterConfiguration.OutputTray</c>.</summary>
        public required OutputTray OutputTray { get; set; }

        /// <summary>From <c>EvolisPrinterConfiguration.ErrorTray</c> — where a spoiled/rejected card goes (step 10).</summary>
        public required OutputTray ErrorTray { get; set; }

        /// <summary>From <c>EvolisPrinterConfiguration.MagCoercivity</c>.</summary>
        public required MagCoercivity MagCoercivity { get; set; }

        public required PrintWay PrintWay { get; set; }
        public required PrintedFace PrintedFace { get; set; }
        public required int X { get; set; }
        public required int Y { get; set; }
        public required string FontFamily { get; set; }
        public required int FontSize { get; set; }
        public required string PrintColor { get; set; }
        public required string BackgroundColor { get; set; }
        public required string FontStyle { get; set; }
    }
}
