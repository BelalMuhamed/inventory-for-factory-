using System;

namespace invetoryBackGroundServices.Machine.Rendering
{
    /// <summary>
    /// Thrown by <see cref="ICardRenderer.Render"/> when the cardholder name violates a hard
    /// business rule — currently: English letters only (no Arabic/RTL names), since
    /// <c>SkiaCardRenderer</c> does not shape or bidi-reorder Arabic text correctly (see
    /// <c>CLAUDE.md</c>'s Phase 4 section for the live finding that led to this rule). A future,
    /// separate phase is expected to make this configurable per product instead of a blanket
    /// rule — this exception exists so the caller gets a clear, specific rejection in the
    /// meantime rather than a silently-wrong printed card.
    /// </summary>
    public sealed class InvalidCardHolderNameException : Exception
    {
        /// <summary>Creates the exception with the reason the name was rejected.</summary>
        public InvalidCardHolderNameException(string message) : base(message) { }
    }
}
