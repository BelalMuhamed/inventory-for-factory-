using System.Collections.Generic;
using System.Text;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.Contracts;

namespace ApplicationLayer.CardFiles
{
    /// <summary>
    /// Default <see cref="ICardFileWriter"/> implementation (Card File Generation, Phase 9.4).
    /// Pure logic — no DB, no I/O, no crypto — so it lives in ApplicationLayer directly, for the
    /// same reason <c>BatchRowParser</c> does: there is no external dependency to abstract away,
    /// and the interface exists for DI and testability rather than for layering.
    /// <para>
    /// Every constant it emits comes from <see cref="BatchFileFormat"/>, shared with the parser.
    /// The round-trip test that matters is write → encrypt → decrypt → parse, asserting zero
    /// failed rows.
    /// </para>
    /// </summary>
    public sealed class CardFileWriter : ICardFileWriter
    {
        /// <inheritdoc />
        public string Write(IReadOnlyList<CardFileLine> lines)
        {
            if (lines is null || lines.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(BatchFileFormat.LineSeparator);
                }

                CardFileLine line = lines[i];

                builder.Append(line.Pan)
                       .Append(BatchFileFormat.FieldDelimiter)
                       .Append(line.ProductName)
                       .Append(BatchFileFormat.FieldDelimiter)
                       .Append(line.BranchName);
            }

            return builder.ToString();
        }
    }
}
