using System;
using invetoryBackGroundServices.Machine.Rendering;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>Outcome of <see cref="EvolisRibbonCompatibilityChecker.Check"/>.</summary>
    public enum RibbonCompatibility
    {
        /// <summary>The loaded ribbon matches what the product expects and can render the needed panel.</summary>
        Ok,

        /// <summary>The loaded ribbon is not the one the product's print configuration expects.</summary>
        Mismatch,

        /// <summary>The job needs a colour panel but the loaded ribbon has no YMC panels.</summary>
        CannotPrintColour
    }

    /// <summary>Result of the three-way check, carrying both ribbon names for the error message.</summary>
    public sealed record RibbonCompatibilityResult(RibbonCompatibility Status, string LoadedRibbonSdkCode, string ExpectedRibbonSdkCode);

    /// <summary>
    /// The Q-25 three-way ribbon compatibility check (Evolis Integration Plan §10, step 6) —
    /// vendor-neutral: both ribbon codes are already plain <c>RibbonTypes.SdkCode</c>-shaped
    /// strings by the time they reach here (<c>Evolis.RibbonType</c> is stringified at the
    /// connection boundary), and <see cref="CardPanel"/> is the renderer's own vendor-neutral
    /// enum. Executed in practice after rendering, not before, despite the plan's step numbering
    /// placing it ahead of the render step — see CLAUDE.md's Phase 5 section for why: the rule
    /// itself reads <c>renderResult.Panel</c>, which can't exist before the render runs, and
    /// rendering alone has no physical side effect worth ordering around (nothing is wasted until
    /// the actual print in step 8).
    /// </summary>
    public sealed class EvolisRibbonCompatibilityChecker
    {
        /// <summary>Runs the check. See the plan's Q-25 reference for the two reject rules.</summary>
        public RibbonCompatibilityResult Check(string loadedRibbonSdkCode, string expectedRibbonSdkCode, CardPanel neededPanel)
        {
            if (!string.Equals(loadedRibbonSdkCode, expectedRibbonSdkCode, StringComparison.OrdinalIgnoreCase))
            {
                return new RibbonCompatibilityResult(RibbonCompatibility.Mismatch, loadedRibbonSdkCode, expectedRibbonSdkCode);
            }

            if (neededPanel == CardPanel.Colour
                && !loadedRibbonSdkCode.Contains("YMC", StringComparison.OrdinalIgnoreCase))
            {
                return new RibbonCompatibilityResult(RibbonCompatibility.CannotPrintColour, loadedRibbonSdkCode, expectedRibbonSdkCode);
            }

            return new RibbonCompatibilityResult(RibbonCompatibility.Ok, loadedRibbonSdkCode, expectedRibbonSdkCode);
        }
    }
}
