namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Extracts the first-16-characters PAN from raw track 2 data (Evolis Integration Plan §10,
    /// step 5). Separate interface so it's independently testable without a card reader.
    /// </summary>
    public interface IPanExtractor
    {
        /// <summary>Returns the PAN exactly as Backend Call #1 expects it.</summary>
        string Extract(string rawTrack2Data);
    }
}
