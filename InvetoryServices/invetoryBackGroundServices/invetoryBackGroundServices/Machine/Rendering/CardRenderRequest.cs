using invetoryBackGroundServices.Enums;

namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// Everything <see cref="ICardRenderer"/> needs to turn one cardholder name into a bitmap
    /// (Evolis Integration Plan §9) — a flattened projection of the Inventory API's
    /// <c>EvolisProductPrintConfiguration</c>. <see cref="RibbonTypeId"/> and
    /// <see cref="ImageId"/> are deliberately absent: the renderer has no printer and no image
    /// store, so neither is meaningful here (ribbon compatibility is Phase 5's concern, consuming
    /// <see cref="CardRenderResult.Panel"/>; a background image is out of this phase's scope).
    /// </summary>
    public sealed record CardRenderRequest(
        string CardHolderName,
        PrintWay PrintWay,
        int X,
        int Y,
        PrintedFace PrintedFace,
        string FontFamily,
        int FontSize,
        string PrintColor,
        string BackgroundColor,
        string FontStyle);
}
