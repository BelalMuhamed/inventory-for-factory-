namespace DomainLayer.Enums
{
    /// <summary>
    /// Lifecycle state of a branch stock request (ERD §4.1 column <c>RequestStatus</c>, extended
    /// by API §4.9 decisions Q-04/Q-05). Persisted as <c>TINYINT</c>.
    /// <para>
    /// The first four values are the ERD's own numbering and keep their original meaning
    /// unchanged. Values 4–6 are appended (decision D-02) so a fresh migration never has to
    /// renumber an existing value — the same approach <see cref="TransactionStatus"/> already
    /// took when it appended <c>PartiallyReceived = 3</c> and <c>Disposed = 4</c>.
    /// </para>
    /// <para>
    /// Status is never assigned ad hoc by application code except for the two terminal closures
    /// (<see cref="Refused"/>, <see cref="Cancelled"/>). Every other transition is produced by
    /// <see cref="Entities.BranchRequest.RecomputeStatus"/>, a pure function of the line counters
    /// (decision D-03) — so the values below describe reachable <em>states</em>, not steps in a
    /// fixed sequence a caller is guaranteed to walk through one at a time.
    /// <b>Correction, superseding an earlier (incorrect) note here:</b> a confirm call can no
    /// longer move a request straight to <see cref="Fulfilled"/> regardless of whether its lines
    /// are Known-way or Unknown-way — the Unknown-way Maker-Checker workflow means a confirm only
    /// stages transfers now, it never settles one, so
    /// <see cref="Entities.BranchRequestItem.ReceivedQuantity"/>-driven crediting (and therefore
    /// <see cref="Fulfilled"/>) always happens later, at a separate <c>receive</c> call on the
    /// generated transfer.
    /// </para>
    /// </summary>
    public enum BranchRequestStatus : byte
    {
        /// <summary>Raised; nothing has been dispatched against any line yet. ERD value 0.</summary>
        InProgress = 0,

        /// <summary>
        /// Every line has been dispatched at least up to its asked quantity, and nothing has
        /// been received yet. ERD value 1.
        /// </summary>
        Confirmed = 1,

        /// <summary>Closed without confirming anything. Terminal. ERD value 2.</summary>
        Refused = 2,

        /// <summary>Closed by the requester before it was (fully) confirmed. Terminal. ERD value 3.</summary>
        Cancelled = 3,

        /// <summary>
        /// Some dispatch has happened (at least one confirm call), but not every line has yet
        /// reached its asked quantity.
        /// <para>
        /// <b>Schema note:</b> not in the original ERD §8 enum list; added with decision D-02
        /// (approved). Flagged for DBA review.
        /// </para>
        /// </summary>
        PartiallyConfirmed = 4,

        /// <summary>
        /// Some quantity has actually been received by the requesting branch, but not every line
        /// has reached its asked quantity yet.
        /// <para>
        /// <b>Schema note:</b> not in the original ERD §8 enum list; added with decision D-02
        /// (approved). Flagged for DBA review.
        /// </para>
        /// </summary>
        PartiallyFulfilled = 5,

        /// <summary>
        /// Every line has received at least its asked quantity. Terminal.
        /// <para>
        /// <b>Schema note:</b> not in the original ERD §8 enum list; added with decision D-02
        /// (approved). Flagged for DBA review.
        /// </para>
        /// </summary>
        Fulfilled = 6
    }
}
