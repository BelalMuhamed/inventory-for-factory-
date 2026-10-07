using invetoryBackGroundServices.Enums;

namespace invetoryBackGroundServices.Machine.Abstractions
{
    /// <summary>
    /// Vendor-neutral print job request (Evolis Integration Plan §10) — filled in for real this
    /// phase; Phase 3 only needed its shape. Mirrors <see cref="Rendering.CardRenderRequest"/>'s
    /// layout fields plus everything <c>EvolisCardJobOrchestrator</c>'s steps need to identify the
    /// device, apply its configuration, and resolve/record the print with the Inventory API.
    /// Built by the orchestrator from <c>Params.EvolisPrintReqDto</c> — the two are almost
    /// identical, but this type is vendor-neutral (no reference to Evolis-specific request/
    /// response shapes) while the DTO is the HTTP wire contract.
    /// </summary>
    public sealed record CardJob(
        string DeviceName,
        string CardHolderName,
        long ProductId,
        long BranchId,
        long RibbonTypeId,
        string RibbonSdkCode,
        InputTray InputTray,
        OutputTray OutputTray,
        OutputTray ErrorTray,
        MagCoercivity MagCoercivity,
        PrintWay PrintWay,
        PrintedFace PrintedFace,
        int X,
        int Y,
        string FontFamily,
        int FontSize,
        string PrintColor,
        string BackgroundColor,
        string FontStyle);

    /// <summary>
    /// Vendor-neutral print job outcome — filled in for real this phase. Mirrors
    /// <c>Common.PrintResult</c>'s distinction between the physical outcome and whether the
    /// Inventory API confirmed it (the reliability plan's "printed but could not confirm" case).
    /// </summary>
    /// <param name="Printed">True when the physical print operation succeeded.</param>
    /// <param name="ResultConfirmedWithBackend">
    /// True once Backend Call #2 confirmed with the Inventory API. False means
    /// <see cref="Printed"/> is known and final, but the Inventory API's record of it could not be
    /// confirmed after retrying — the caller should treat this as needing manual follow-up.
    /// </param>
    /// <param name="ProductItemId">The resolved card's id, or null if the job failed before resolve-for-print returned one.</param>
    /// <param name="IdempotencyKey">The key used for Backend Call #2, or null if the job failed before one was generated.</param>
    /// <param name="FailureReason">Human-readable reason the job failed, or null on success.</param>
    public sealed record CardJobResult(
        bool Printed,
        bool ResultConfirmedWithBackend,
        long? ProductItemId,
        string? IdempotencyKey,
        string? FailureReason);
}
