using System.Collections.Generic;

namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// Output of <see cref="ICardRenderer.Render"/> — a BMP plus the panel it belongs on (Evolis
    /// Integration Plan §9). <see cref="Warnings"/> carries every non-fatal degradation the
    /// renderer applied (missing font, out-of-bounds position, non-white background on a resin
    /// panel) so the caller can surface them to the operator rather than let them pass silently —
    /// plan §9's own rule for each case.
    /// </summary>
    public sealed record CardRenderResult(
        byte[] BmpBytes,
        CardPanel Panel,
        int Width,
        int Height,
        IReadOnlyList<string> Warnings);
}
