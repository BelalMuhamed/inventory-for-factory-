namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// A batch-file row that passed format validation (arity, PAN length/Luhn, no in-file
    /// duplicate). Not yet validated against the tenant's product/branch catalog — that's the
    /// orchestrator's job (Phase 6), which turns a <see cref="ParsedBatchRow"/> into either a new
    /// <c>ProductItem</c> or a <see cref="FailedBatchRow"/> with
    /// <see cref="FailureReason.UnknownProduct"/>/<see cref="FailureReason.UnknownBranch"/>.
    /// </summary>
    /// <param name="RowNumber">1-based line number in the decrypted file, for diagnostics.</param>
    /// <param name="Pan">The full, cleartext PAN. Never logged or returned to the client as-is.</param>
    /// <param name="ProductName">Product name as written in the file (not yet resolved to an id).</param>
    /// <param name="BranchName">Branch name as written in the file (not yet resolved to an id).</param>
    public sealed record ParsedBatchRow(int RowNumber, string Pan, string ProductName, string BranchName);
}
