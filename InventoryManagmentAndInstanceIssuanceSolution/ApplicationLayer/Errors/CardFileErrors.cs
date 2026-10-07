using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ApplicationLayer.DTOs.CardFiles;
using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Stable, localizable <see cref="Error"/> catalogue for card-file generation (Card File
    /// Generation, Phase 9.2). Mirrors <see cref="BatchErrors"/>'s pattern.
    /// <para>
    /// Tenant-existence failures deliberately reuse <see cref="TenantErrors"/> rather than
    /// duplicating codes here: a missing tenant is a missing tenant, and clients already handle
    /// <c>Tenant.NotFound</c>.
    /// </para>
    /// </summary>
    public static class CardFileErrors
    {
        /// <summary>
        /// The caller is not a system admin (→ 401). The <c>SystemAdminOnly</c> policy normally
        /// stops this at the pipeline; the service re-checks as defence in depth, matching
        /// <c>ProductService</c>/<c>BranchService</c>.
        /// </summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("CardFile.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>
        /// The target tenant is soft-deleted or deactivated (→ 409). Separate from
        /// <c>Tenant.NotFound</c> because "exists but cannot receive cards" is an operationally
        /// different situation from "does not exist".
        /// </summary>
        public static Error TenantUnavailable(long tenantId) =>
            Error.Conflict(
                "CardFile.TenantUnavailable",
                $"Tenant {tenantId} is inactive or deleted and cannot be issued a card file.")
                .WithArg(tenantId.ToString(CultureInfo.InvariantCulture));

        /// <summary>The request contains no cards (→ 422).</summary>
        public static Error NoCards() =>
            Error.Validation("CardFile.NoCards", "At least one card is required.");

        /// <summary>The request exceeds the configured card cap (→ 422).</summary>
        public static Error TooManyCards(int maximum) =>
            Error.Validation(
                "CardFile.TooManyCards",
                $"A card file may contain at most {maximum} cards.")
                .WithArg(maximum.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// One or more cards failed validation (→ 422). All-or-nothing by design: a card file is
        /// a deliverable artifact, and shipping one that is knowingly part-broken costs more than
        /// a validation round-trip. Nothing is generated when this fires.
        /// <para>
        /// The per-card detail rides on <see cref="Error.Details"/>, keyed by the card's position
        /// in the request (<c>"cards[7]"</c>) so the caller can map each rejection straight back
        /// to its input. Reasons are machine-readable enum names, not prose — the caller here is
        /// an admin tool, not an end user reading a message. PANs never appear.
        /// </para>
        /// </summary>
        /// <param name="rejections">Every card that failed, in request order.</param>
        public static Error CardsRejected(IReadOnlyList<RejectedCardEntry> rejections)
        {
            IReadOnlyDictionary<string, string[]> details = rejections.ToDictionary(
                rejection => $"cards[{rejection.Index}]",
                rejection => new[] { rejection.Reason.ToString() });

            return Error.Validation(
                    "CardFile.CardsRejected",
                    $"{rejections.Count} card(s) failed validation. No file was generated.")
                .WithArg(rejections.Count.ToString(CultureInfo.InvariantCulture))
                .WithDetails(details);
        }

        /// <summary>
        /// An unexpected exception was caught at the generation boundary (→ 500). Logged with
        /// tenant/trace context — never with card data — before the opaque message is surfaced.
        /// </summary>
        public static Error GenerationFailed() =>
            Error.Internal(
                "CardFile.GenerationFailed",
                "An unexpected error occurred while generating the card file. Reference the trace id when reporting this.");
    }
}
