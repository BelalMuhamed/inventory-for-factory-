namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// Turns a cardholder name plus a flattened print configuration into a printable bitmap
    /// (Evolis Integration Plan §9) — a pure function from inputs to bytes, self-contained and
    /// fully testable without hardware or a printer. <c>SkiaCardRenderer</c> is the only
    /// implementation in this phase.
    /// </summary>
    public interface ICardRenderer
    {
        /// <summary>
        /// Renders one card. Never throws for a malformed-but-plausible layout input — degrades
        /// and warns instead (see <see cref="CardRenderResult.Warnings"/>). The one exception:
        /// <see cref="InvalidCardHolderNameException"/> for a cardholder name containing anything
        /// other than English letters/digits/basic punctuation — not a layout quirk to degrade
        /// past, but a hard business rule (Arabic/RTL names are not supported by this renderer
        /// yet; see <c>CLAUDE.md</c>'s Phase 4 section).
        /// </summary>
        CardRenderResult Render(CardRenderRequest request);
    }
}
