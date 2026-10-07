namespace invetoryBackGroundServices.Common
{
    /// <summary>
    /// Result of a print attempt, returned inside <see cref="ApiResponse{T}"/>. Distinguishes the
    /// physical outcome from whether the Inventory API confirmed it - see
    /// <see cref="ResultConfirmedWithBackend"/> for the "printed but could not confirm" case the
    /// reliability plan calls out specifically.
    /// </summary>
    /// <param name="Printed">True when the physical emboss operation succeeded.</param>
    /// <param name="ResultConfirmedWithBackend">
    /// True once Backend Call #2 confirmed with the Inventory API. False means the physical
    /// outcome (see <see cref="Printed"/>) is known and final, but the Inventory API's record of
    /// it could not be confirmed after retrying - the caller should treat this as needing manual
    /// follow-up, not as an ordinary failure.
    /// </param>
    /// <param name="ProductItemId">The resolved card's id, for correlating with the Inventory API.</param>
    /// <param name="IdempotencyKey">The key used for Backend Call #2 - useful if manual follow-up is needed.</param>
    public sealed record PrintResult(
        bool Printed, bool ResultConfirmedWithBackend, long ProductItemId, string IdempotencyKey);
}
