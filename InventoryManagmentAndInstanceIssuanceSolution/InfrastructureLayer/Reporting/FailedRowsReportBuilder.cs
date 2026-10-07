using System.Collections.Generic;
using System.IO;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.Contracts;
using ClosedXML.Excel;

namespace InfrastructureLayer.Reporting
{
    /// <summary>
    /// ClosedXML implementation of <see cref="IFailedRowsReportBuilder"/> (Batch Upload Phased
    /// Plan, Phase 5). A genuine external-library integration, unlike the pure
    /// <c>BatchRowParser</c> (Phase 4) — belongs in InfrastructureLayer per the project's Onion
    /// convention.
    /// </summary>
    public sealed class FailedRowsReportBuilder : IFailedRowsReportBuilder
    {
        private const string WorksheetName = "Failed Rows";
        private const int MaskedPanColumn = 1;
        private const int FailureReasonColumn = 2;
        private const int HeaderRow = 1;

        /// <inheritdoc />
        public byte[] Build(IReadOnlyList<FailedBatchRow> failedRows, FailedRowsReportLabels labels)
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet sheet = workbook.Worksheets.Add(WorksheetName);

            sheet.Cell(HeaderRow, MaskedPanColumn).Value = labels.MaskedPanColumnHeader;
            sheet.Cell(HeaderRow, FailureReasonColumn).Value = labels.FailureReasonColumnHeader;
            sheet.Row(HeaderRow).Style.Font.Bold = true;

            int row = HeaderRow + 1;
            foreach (FailedBatchRow failedRow in failedRows)
            {
                // failedRow.MaskedPan is the only PAN-shaped value FailedBatchRow can carry —
                // there is no real-PAN field to accidentally write here.
                sheet.Cell(row, MaskedPanColumn).Value = failedRow.MaskedPan;
                sheet.Cell(row, FailureReasonColumn).Value = ResolveReasonText(failedRow.Reason, labels.ReasonText);
                row++;
            }

            sheet.Columns(MaskedPanColumn, FailureReasonColumn).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // Falls back to the enum member's own name rather than throwing, so a missing
        // localization entry degrades gracefully instead of breaking report generation.
        private static string ResolveReasonText(FailureReason reason, IReadOnlyDictionary<FailureReason, string> reasonText)
            => reasonText.TryGetValue(reason, out string? text) ? text : reason.ToString();
    }
}
