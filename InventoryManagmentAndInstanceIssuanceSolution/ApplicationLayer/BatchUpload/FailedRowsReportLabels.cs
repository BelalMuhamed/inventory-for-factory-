using System.Collections.Generic;

namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// Already-localized display text for the failed-rows report (Batch Upload Phased Plan,
    /// Phase 5). Deliberately carries only strings, not an <c>IStringLocalizer</c>: the report
    /// builder must not depend on the localization pipeline directly, because the shared
    /// <c>Messages</c> resource type physically lives in the Presentation project — resolving it
    /// here would mean Application/Infrastructure referencing Presentation, which Onion
    /// architecture forbids. The caller (which does have locale context) resolves these strings
    /// and passes them in.
    /// </summary>
    /// <param name="MaskedPanColumnHeader">Localized header for the masked-PAN column.</param>
    /// <param name="FailureReasonColumnHeader">Localized header for the failure-reason column.</param>
    /// <param name="ReasonText">
    /// Localized display text per <see cref="FailureReason"/>. A reason missing from this map
    /// falls back to the enum member's own name rather than failing report generation.
    /// </param>
    public sealed record FailedRowsReportLabels(
        string MaskedPanColumnHeader,
        string FailureReasonColumnHeader,
        IReadOnlyDictionary<FailureReason, string> ReasonText);
}
