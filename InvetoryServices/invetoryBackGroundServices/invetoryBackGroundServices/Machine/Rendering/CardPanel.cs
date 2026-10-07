namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// Which SDK print panel a rendered bitmap belongs on (Evolis Integration Plan §9,
    /// decision Q-25) — the decisive input to Phase 5's ribbon-compatibility check and its choice
    /// between <c>PrintSession.SetBlack</c> and <c>PrintSession.SetImage</c>. Decided here, by the
    /// renderer, from <c>PrintColor</c> alone; never second-guessed downstream.
    /// </summary>
    public enum CardPanel
    {
        /// <summary>Monochrome resin black panel — printed via <c>PrintSession.SetBlack</c>.</summary>
        ResinBlack,

        /// <summary>Full-colour YMC panel — printed via <c>PrintSession.SetImage</c> ("using the YMC panels only").</summary>
        Colour
    }
}
