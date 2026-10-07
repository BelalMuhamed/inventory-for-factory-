using System.Collections.Generic;
using ApplicationLayer.BatchUpload;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Builds the failed-rows Excel report for a batch upload (API §4.8, Batch Upload Phased
    /// Plan Phase 5). Self-contained: takes the already-failed rows and already-localized
    /// display text, produces the workbook bytes. Never receives, and therefore can never emit,
    /// a real PAN — <see cref="FailedBatchRow"/> only ever carries the masked value.
    /// </summary>
    public interface IFailedRowsReportBuilder
    {
        /// <summary>
        /// Builds a two-column ("Masked PAN", "Failure Reason") worksheet, one row per failed
        /// row, in file order.
        /// </summary>
        /// <param name="failedRows">The batch's failed rows, in the order they should appear.</param>
        /// <param name="labels">Localized column headers and per-reason display text.</param>
        /// <returns>The workbook's raw bytes (.xlsx).</returns>
        byte[] Build(IReadOnlyList<FailedBatchRow> failedRows, FailedRowsReportLabels labels);
    }
}
