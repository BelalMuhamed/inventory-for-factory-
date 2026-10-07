namespace ApplicationLayer.DTOs.ProductItems
{
    /// <summary>
    /// Backend Call #1 payload (Matica Print Flow): resolves and validates exactly one physical
    /// card for printing, right after the Printer Agent's <c>ReadMAG</c> step. <paramref name="Pan"/>
    /// is the raw, full PAN read off the magnetic stripe — used only transiently, within this one
    /// request, to compute the card's identity fingerprint; it is never persisted, logged, or
    /// echoed back.
    /// </summary>
    /// <param name="Pan">Raw full PAN as read off the card's magnetic stripe. Never stored as-is.</param>
    /// <param name="ProductId">The product/card type this card is expected to be.</param>
    /// <param name="BranchId">The branch this card is being printed at — must match the Print Agent token's own <c>branchId</c> claim.</param>
    public sealed record ResolveForPrintRequest(string Pan, long ProductId, long BranchId);

    /// <summary>
    /// Successful result of Backend Call #1. Carries only what the Printer Agent needs to identify
    /// the card in Backend Call #2 — never the PAN in any form beyond the already-masked display
    /// value.
    /// </summary>
    /// <param name="ProductItemId">Identity of the resolved card — pass this to Backend Call #2.</param>
    /// <param name="MaskedPan">The card's display-safe masked PAN.</param>
    /// <param name="HolderName">Any cardholder name already on file for this card, or null.</param>
    public sealed record ResolveForPrintResponse(long ProductItemId, string MaskedPan, string? HolderName);

    /// <summary>
    /// Backend Call #2 payload (Matica Print Flow): records the physical outcome of a print
    /// attempt, after the Printer Agent's <c>EjectCard</c> step. Fired exactly once per physical
    /// attempt in the success/failure case, and safely retryable — see
    /// <c>ProductItemService.RecordPrintResultAsync</c>'s doc comment for the lightweight
    /// idempotency behavior this relies on.
    /// </summary>
    /// <param name="BranchId">
    /// The branch this card was printed at — must match the same value passed to Backend Call #1
    /// (and the Print Agent token's own <c>branchId</c> claim). For an Unknown-way card this is
    /// also the branch it gets assigned to for the first time.
    /// </param>
    /// <param name="Success">True when the physical print/emboss succeeded; false when it failed and the card is spoiled.</param>
    /// <param name="HolderName">Cardholder name to record, if applicable.</param>
    /// <param name="IdempotencyKey">
    /// Generated once by the Printer Agent per physical print attempt and reused on every retry of
    /// this same attempt. Not persisted or compared against a stored table (deliberately
    /// lightweight, per the agreed plan) — carried through only for log/audit correlation.
    /// </param>
    public sealed record RecordPrintResultRequest(
        long BranchId, bool Success, string? HolderName, string IdempotencyKey);
}
