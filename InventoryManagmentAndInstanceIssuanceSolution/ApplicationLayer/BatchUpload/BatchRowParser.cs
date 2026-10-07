using System;
using System.Collections.Generic;
using ApplicationLayer.Contracts;
using ApplicationLayer.Security;

namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// Default <see cref="IBatchRowParser"/> implementation (Batch Upload Phased Plan, Phase 4).
    /// Pure logic only — no DB, no I/O — which is why it lives in ApplicationLayer directly
    /// rather than behind an InfrastructureLayer implementation: there is no external dependency
    /// to abstract away. The interface still exists for DI/testability (the orchestrator depends
    /// on the abstraction, not this type, per the project's constructor-injection convention).
    /// <para>
    /// Phase 9.1: the format constants and PAN rules moved to <see cref="BatchFileFormat"/> so the
    /// file writer on the generation side derives from the same definitions. What stays here is
    /// row-level <em>policy</em> — which failure reason applies, and in-file duplicate detection —
    /// as opposed to the format itself.
    /// </para>
    /// </summary>
    public sealed class BatchRowParser : IBatchRowParser
    {
        /// <inheritdoc />
        public (IReadOnlyList<ParsedBatchRow> ValidRows, IReadOnlyList<FailedBatchRow> FailedRows) Parse(string fileContent)
        {
            var validRows = new List<ParsedBatchRow>();
            var failedRows = new List<FailedBatchRow>();

            if (string.IsNullOrEmpty(fileContent))
            {
                return (validRows, failedRows);
            }

            string[] lines = BatchFileFormat.SplitLines(fileContent);
            var seenPans = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < lines.Length; i++)
            {
                // A trailing newline at end-of-file produces one empty final element that is not
                // a real row — drop only that one. A blank line anywhere else is a real
                // (malformed) row and must still be reported.
                if (i == lines.Length - 1 && lines[i].Length == 0)
                {
                    continue;
                }

                int rowNumber = i + 1;
                string[] fields = lines[i].Split(BatchFileFormat.FieldDelimiter);

                if (fields.Length != BatchFileFormat.RequiredFieldCount || HasBlankField(fields))
                {
                    failedRows.Add(new FailedBatchRow(rowNumber, "N/A", FailureReason.MalformedLine));
                    continue;
                }

                string pan = BatchFileFormat.NormalizePan(fields[0]);
                string productName = fields[1].Trim();
                string branchName = fields[2].Trim();

                if (!BatchFileFormat.IsValidPan(pan))
                {
                    failedRows.Add(new FailedBatchRow(rowNumber, PanMasker.Mask(pan), FailureReason.InvalidPan));
                    continue;
                }

                if (!seenPans.Add(pan))
                {
                    failedRows.Add(new FailedBatchRow(rowNumber, PanMasker.Mask(pan), FailureReason.DuplicatePanInFile));
                    continue;
                }

                validRows.Add(new ParsedBatchRow(rowNumber, pan, productName, branchName));
            }

            return (validRows, failedRows);
        }

        private static bool HasBlankField(string[] fields)
        {
            foreach (string field in fields)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
