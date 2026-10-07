using System.Collections.Generic;
using ApplicationLayer.BatchUpload;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Parses a decrypted batch file into valid and failed rows (Batch Upload Phased Plan,
    /// Phase 4). Pure — no DB, no I/O, fully synchronous, fully unit-testable. Row format:
    /// <c>pan|productName|branchName</c>, one row per line (matches the existing upload format).
    /// </summary>
    public interface IBatchRowParser
    {
        /// <summary>
        /// Parses <paramref name="fileContent"/> line by line. Validates field arity, PAN length
        /// (13–19 digits) and the Luhn checksum, and flags intra-file duplicate PANs (only the
        /// first occurrence of a given PAN proceeds; later occurrences fail as
        /// <see cref="FailureReason.DuplicatePanInFile"/>). Does not check the row's product/branch
        /// names against anything — that requires the DB and is the orchestrator's job (Phase 6).
        /// </summary>
        /// <param name="fileContent">The full decrypted file content as text.</param>
        /// <returns>
        /// Every row from the file, partitioned into <c>ValidRows</c> (candidates for import) and
        /// <c>FailedRows</c> (already-terminal failures). <c>ValidRows.Count + FailedRows.Count</c>
        /// equals the file's row count — compare against the caller-declared expected row count
        /// for <c>BatchErrors.ExpectedRowCountMismatch</c>.
        /// </returns>
        (IReadOnlyList<ParsedBatchRow> ValidRows, IReadOnlyList<FailedBatchRow> FailedRows) Parse(string fileContent);
    }
}
