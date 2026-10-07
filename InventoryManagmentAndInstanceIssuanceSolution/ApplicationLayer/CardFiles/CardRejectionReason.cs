namespace ApplicationLayer.CardFiles
{
    /// <summary>
    /// Why a single card in a generation request was rejected (Card File Generation, Phase 9.4).
    /// <para>
    /// Deliberately distinct from <c>FailureReason</c>, which classifies rows read <em>from</em> a
    /// file. The two sets overlap but are not the same: there is no "malformed line" when the
    /// server is the one writing the lines, and conversely a caller can supply a name containing
    /// the field delimiter, which no well-formed file row ever can.
    /// </para>
    /// </summary>
    public enum CardRejectionReason
    {
        /// <summary>PAN is not 13–19 digits, or fails the Luhn checksum.</summary>
        InvalidPan = 0,

        /// <summary>The same PAN appears more than once in the request.</summary>
        DuplicatePan = 1,

        /// <summary>Product name or branch name is missing or blank.</summary>
        MissingField = 2,

        /// <summary>A name contains the field delimiter or a line break, which would corrupt the row.</summary>
        ForbiddenCharacter = 3,

        /// <summary>No active product with that name exists for the target tenant.</summary>
        UnknownProduct = 4,

        /// <summary>No active branch with that name exists for the target tenant.</summary>
        UnknownBranch = 5
    }
}
