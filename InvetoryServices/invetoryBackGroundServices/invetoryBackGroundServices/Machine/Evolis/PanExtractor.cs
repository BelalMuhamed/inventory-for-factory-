using System;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// <see cref="IPanExtractor"/> default implementation (Evolis Integration Plan §10, step 5).
    /// <para>
    /// Open question this phase could not resolve live (see CLAUDE.md's Phase 5 section): every
    /// card available for a real magnetic-read probe during this phase came back
    /// <c>MAG_EBLANK</c> (no magnetic data encoded at all), so whether a real encoded card from
    /// this deployment carries ISO 7813 sentinels (<c>;</c>...<c>=</c>/<c>?</c>) around the PAN, or
    /// stores the PAN as bare digits the way Matica's cards apparently do (its own extractor is a
    /// plain <c>Substring(0, 16)</c>, no sentinel handling at all), was never confirmed against a
    /// real encoded card. This implementation handles both shapes defensively rather than
    /// guessing one: it strips a leading ISO start sentinel and anything from the first
    /// <c>=</c>/<c>?</c> onward if present, then takes the first 16 characters of what remains —
    /// a strict superset of Matica's own logic (a no-op pass-through when no sentinels exist, so
    /// behavior for Matica-shaped data is unchanged). Must be re-verified against a real encoded
    /// card once one is available.
    /// </para>
    /// </summary>
    public sealed class PanExtractor : IPanExtractor
    {
        private const int PanLength = 16;
        private static readonly char[] FieldSentinels = { '=', '?' };

        /// <inheritdoc />
        public string Extract(string rawTrack2Data)
        {
            string data = (rawTrack2Data ?? string.Empty).Trim();

            if (data.Length > 0 && data[0] == ';')
            {
                data = data[1..];
            }

            int sentinelIndex = data.IndexOfAny(FieldSentinels);
            if (sentinelIndex >= 0)
            {
                data = data[..sentinelIndex];
            }

            return data.Length >= PanLength ? data[..PanLength] : data;
        }
    }
}
