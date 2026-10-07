namespace ApplicationLayer.CardFiles
{
    /// <summary>
    /// One fully-validated, ready-to-serialize card row (Card File Generation, Phase 9.4). The
    /// mirror image of <c>ParsedBatchRow</c>: that type is what comes <em>out</em> of a file,
    /// this is what goes <em>in</em>.
    /// <para>
    /// Constructing one is an assertion that the row has already passed every check — PAN shape
    /// and Luhn, non-blank names, no delimiter or line-break characters, and product/branch names
    /// resolved against the target tenant. <c>CardFileWriter</c> does not re-validate; it
    /// serializes.
    /// </para>
    /// </summary>
    /// <param name="Pan">Normalized clear PAN (13–19 digits, Luhn-valid).</param>
    /// <param name="ProductName">Canonical product name as stored for the tenant, not the caller's casing.</param>
    /// <param name="BranchName">Canonical branch name as stored for the tenant, not the caller's casing.</param>
    public sealed record CardFileLine(string Pan, string ProductName, string BranchName);
}
