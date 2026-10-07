namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// CR-80 card geometry at 300 dpi (Evolis Integration Plan §9, confirmed against the Evolis
    /// Consumables table and Phase 0's hardware probe). Shared by every renderer, vendor-neutral
    /// in itself even though only Evolis uses it today.
    /// </summary>
    public static class CardGeometry
    {
        /// <summary>Print resolution, dots per inch.</summary>
        public const int Dpi = 300;

        /// <summary>Canvas width in pixels for <see cref="Enums.PrintWay.Landscape"/>, height for <see cref="Enums.PrintWay.Portrait"/>.</summary>
        public const int LongSidePx = 1016;

        /// <summary>Canvas height in pixels for <see cref="Enums.PrintWay.Landscape"/>, width for <see cref="Enums.PrintWay.Portrait"/>.</summary>
        public const int ShortSidePx = 648;

        /// <summary>Converts a point size (as stored on <c>EvolisProductPrintConfiguration.FontSize</c>) to pixels at <see cref="Dpi"/>.</summary>
        public static int PointsToPixels(int points) => points * Dpi / 72;
    }
}
