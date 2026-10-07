namespace DomainLayer.Common
{
    /// <summary>
    /// Classifies a failure so the presentation layer can deterministically map a
    /// <see cref="Result"/> outcome to an HTTP status code without inspecting message text.
    /// Mapping (see API Spec §2.5):
    /// <list type="bullet">
    ///   <item><description><see cref="Validation"/> → 422 (request body fails domain rules).</description></item>
    ///   <item><description><see cref="Conflict"/> → 409 (business rule violation / concurrent update conflict).</description></item>
    ///   <item><description><see cref="NotFound"/> → 404 (resource missing, or belongs to another tenant).</description></item>
    ///   <item><description><see cref="Unauthorized"/> → 401 (missing/expired/invalid credentials).</description></item>
    ///   <item><description><see cref="Forbidden"/> → 403 (authenticated but tenant mismatch).</description></item>
    /// </list>
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>No failure. Reserved for <see cref="Error.None"/> on successful results.</summary>
        None = 0,

        /// <summary>Request data violates a domain rule. Maps to HTTP 422.</summary>
        Validation = 1,

        /// <summary>Business-rule or concurrency conflict. Maps to HTTP 409.</summary>
        Conflict = 2,

        /// <summary>Requested resource does not exist (or is not visible to the caller). Maps to HTTP 404.</summary>
        NotFound = 3,

        /// <summary>Caller is not authenticated. Maps to HTTP 401.</summary>
        Unauthorized = 4,

        /// <summary>Caller is authenticated but lacks access to the resource. Maps to HTTP 403.</summary>
        Forbidden = 5,

        /// <summary>
        /// An unexpected failure caught and logged with rich context by the caller (rather than
        /// left to the generic unhandled-exception middleware), then surfaced as an opaque
        /// message. Maps to HTTP 500. Introduced for the batch-upload pipeline's own boundary
        /// catch (Batch Upload Phased Plan, Phase 6) — Serilog logging happens where the failure
        /// occurs, with tenant/trace/batch context, not generically in the middleware.
        /// </summary>
        Internal = 6
    }
}
