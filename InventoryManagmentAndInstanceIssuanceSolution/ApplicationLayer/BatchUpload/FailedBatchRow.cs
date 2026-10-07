namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// A batch-file row that will not become a <c>ProductItem</c>. Carries only the masked PAN —
    /// never the real one — so it is safe to hold in memory, log, and write into the Phase 5
    /// failed-rows Excel report.
    /// </summary>
    /// <param name="RowNumber">1-based line number in the decrypted file.</param>
    /// <param name="MaskedPan">
    /// The masked PAN (ten mask characters + last six digits), or the literal <c>"N/A"</c> when
    /// the line was too malformed to extract a PAN at all.
    /// </param>
    /// <param name="Reason">Why the row failed.</param>
    public sealed record FailedBatchRow(int RowNumber, string MaskedPan, FailureReason Reason);
}
