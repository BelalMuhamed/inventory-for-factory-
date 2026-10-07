using System.Collections.Generic;
using ApplicationLayer.CardFiles;

namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Serializes validated card rows into batch-file plaintext (Card File Generation,
    /// Phase 9.4) — the inverse of <c>IBatchRowParser</c>.
    /// </summary>
    public interface ICardFileWriter
    {
        /// <summary>
        /// Renders <paramref name="lines"/> as batch-file plaintext in the layout defined by
        /// <c>BatchFileFormat</c>.
        /// </summary>
        /// <param name="lines">Validated rows, emitted in the order supplied.</param>
        /// <returns>
        /// The file content, ready to hash and encrypt. No trailing line separator: the parser
        /// tolerates one, but omitting it keeps the row count exactly equal to
        /// <c>lines.Count</c>, which is the number the tenant must supply as
        /// <c>expectedRowCount</c> on upload.
        /// </returns>
        string Write(IReadOnlyList<CardFileLine> lines);
    }
}
