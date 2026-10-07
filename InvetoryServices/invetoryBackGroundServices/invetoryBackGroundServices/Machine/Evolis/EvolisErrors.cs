using invetoryBackGroundServices.Common;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Catalogue of every failure the Evolis adapter can report (Evolis Integration Plan §8/§13).
    /// Mirrors <see cref="MachineErrors"/>'s factory-method shape exactly — same
    /// <see cref="MachineError"/> type, same resx-key-doubles-as-code convention — so this
    /// adapter's failures flow through the existing <see cref="Middleware.GlobalExceptionMiddleware"/>
    /// and <c>Messages.resx</c>/<c>Messages.ar.resx</c> unchanged.
    /// </summary>
    public static class EvolisErrors
    {
        /// <summary>The printer did not respond within the configured timeout.</summary>
        public static MachineError Timeout(string? detail = null) => new(
            "Evolis.Timeout", MachineErrorCategory.Timeout,
            "The Evolis printer did not respond within the configured timeout.", detail);

        /// <summary>The printer or its session is currently busy with another operation.</summary>
        public static MachineError Busy(string? detail = null) => new(
            "Evolis.Busy", MachineErrorCategory.MachineRejected,
            "The Evolis printer is busy.", detail);

        /// <summary>Could not communicate with the printer at all.</summary>
        public static MachineError CommunicationFailure(string? detail = null) => new(
            "Evolis.CommunicationFailure", MachineErrorCategory.CommunicationFailure,
            "Could not communicate with the Evolis printer. Check that it is powered on and reachable.", detail);

        /// <summary>A network-level TLS/certificate error occurred talking to the printer.</summary>
        public static MachineError CertificateError(string? detail = null) => new(
            "Evolis.CertificateError", MachineErrorCategory.CommunicationFailure,
            "A certificate error occurred communicating with the Evolis printer.", detail);

        /// <summary>The printer needs a physical action from the operator before it can continue.</summary>
        public static MachineError NeedsAction(string? detail = null) => new(
            "Evolis.NeedsAction", MachineErrorCategory.MachineRejected,
            "The Evolis printer needs attention before it can continue.", detail);

        /// <summary>A mechanical fault occurred — requires physical service, not a retry.</summary>
        public static MachineError MechanicalFailure(string? detail = null) => new(
            "Evolis.MechanicalFailure", MachineErrorCategory.MachineRejected,
            "The Evolis printer reported a mechanical fault.", detail);

        /// <summary>The ribbon loaded in the printer is not a known/supported type.</summary>
        public static MachineError UnknownRibbon(string? detail = null) => new(
            "Evolis.UnknownRibbon", MachineErrorCategory.MachineRejected,
            "The Evolis printer's loaded ribbon is not recognized.", detail);

        /// <summary>
        /// Evolis Integration Plan §10 step 6 (Q-25), case 1: the ribbon loaded in the printer is
        /// not the one the product's print configuration expects. Names both ribbons so the
        /// operator knows what to load, per Q-25's "clear, localized, actionable" requirement.
        /// </summary>
        public static MachineError RibbonMismatch(string expectedRibbonSdkCode, string loadedRibbonSdkCode) => new(
            "Evolis.RibbonMismatch", MachineErrorCategory.MachineRejected,
            "The loaded ribbon does not match this product's configured ribbon.",
            $"expected '{expectedRibbonSdkCode}', found '{loadedRibbonSdkCode}'.");

        /// <summary>Evolis Integration Plan §10 step 6 (Q-25), case 2: the job needs a colour panel but the loaded ribbon has no YMC panels.</summary>
        public static MachineError RibbonCannotPrintColour(string loadedRibbonSdkCode) => new(
            "Evolis.RibbonCannotPrintColour", MachineErrorCategory.MachineRejected,
            "This card requires a colour ribbon, but the loaded ribbon cannot print colour.",
            $"loaded ribbon: '{loadedRibbonSdkCode}'.");

        /// <summary>
        /// The product's configured ribbon has no corresponding
        /// <c>SettingsKey.GRibbonType</c> token on this SDK version — currently only
        /// <c>Evolis.RibbonType.CLEAR</c> (confirmed live; see
        /// <see cref="EvolisPrintSettingsMapper"/>'s remarks). Not a hardware state, so distinct
        /// from <see cref="RibbonMismatch"/>/<see cref="RibbonCannotPrintColour"/> — this product's
        /// print configuration cannot be printed on this SDK version at all, regardless of what's
        /// physically loaded.
        /// </summary>
        public static MachineError RibbonNotSelectable(string ribbonSdkCode) => new(
            "Evolis.RibbonNotSelectable", MachineErrorCategory.Internal,
            "This product's configured ribbon cannot be selected for printing on this printer.",
            $"ribbon: '{ribbonSdkCode}'.");

        /// <summary>
        /// A second job for the same device was already running (Evolis Integration Plan §10,
        /// "Concurrency") — fails fast rather than queuing, since the SDK already serializes
        /// access to one physical printer via session management and <c>Reserve()</c>.
        /// </summary>
        public static MachineError PrinterBusy() => new(
            "Evolis.PrinterBusy", MachineErrorCategory.MachineRejected,
            "This Evolis printer is already busy with another print job.");

        /// <summary>
        /// The supplied cardholder name is not English-only (see <c>InvalidCardHolderNameException</c>,
        /// CLAUDE.md's Phase 4 follow-up section). Raised before any card is touched — step 0 of
        /// the orchestrator runs the renderer first, specifically so this never wastes a card or a
        /// backend call.
        /// </summary>
        public static MachineError InvalidCardHolderName(string? detail = null) => new(
            "Evolis.InvalidCardHolderName", MachineErrorCategory.Validation,
            "The cardholder name must contain only English letters, digits, and basic punctuation.", detail);

        /// <summary>The magnetic track read came back blank (card not encoded).</summary>
        public static MachineError BlankTrack(string? detail = null) => new(
            "Evolis.BlankTrack", MachineErrorCategory.MachineRejected,
            "The card's magnetic track is blank.", detail);

        /// <summary>The magnetic track read/write failed (unreadable or corrupt data).</summary>
        public static MachineError MagneticError(string? detail = null) => new(
            "Evolis.MagneticError", MachineErrorCategory.MachineRejected,
            "The Evolis printer could not read or write the card's magnetic track.", detail);

        /// <summary>Caller is not authorized for this printer/operation.</summary>
        public static MachineError Forbidden(string? detail = null) => new(
            "Evolis.Forbidden", MachineErrorCategory.Forbidden,
            "Not authorized to perform this operation on the Evolis printer.", detail);

        /// <summary>The operation was cancelled (expected outcome of an operator cancel action, not a failure).</summary>
        public static MachineError Cancelled() => new(
            "Evolis.Cancelled", MachineErrorCategory.MachineRejected,
            "The operation was cancelled.");

        /// <summary>
        /// Anything else, including SDK return codes this catalogue does not specifically
        /// recognize (plan §13.2 rule: an unmapped code degrades to Internal + log, it never
        /// throws an unhandled exception) and genuine bugs in this adapter.
        /// </summary>
        public static MachineError Internal(string? detail = null) => new(
            "Evolis.InternalError", MachineErrorCategory.Internal,
            "An internal error occurred in the Evolis printer adapter.", detail);
    }
}
