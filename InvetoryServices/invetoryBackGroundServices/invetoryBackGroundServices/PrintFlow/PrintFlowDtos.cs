namespace invetoryBackGroundServices.PrintFlow
{
    /// <summary>
    /// Mirrors the Inventory API's <c>ApiResponse&lt;T&gt;</c> envelope — every endpoint there
    /// responds with the same <c>{ success, data, error }</c> shape on both success and failure.
    /// </summary>
    public sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public ApiErrorEnvelope? Error { get; set; }
    }

    /// <summary>Mirrors the Inventory API's <c>ApiError</c> shape.</summary>
    public sealed class ApiErrorEnvelope
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? Category { get; set; }
    }

    /// <summary>Backend Call #1 request body — matches the Inventory API's <c>ResolveForPrintRequest</c>.</summary>
    public sealed class ResolveForPrintRequestDto
    {
        /// <summary>Raw full PAN as read off the magnetic stripe. Sent once, over TLS, never logged.</summary>
        public string Pan { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public long BranchId { get; set; }
    }

    /// <summary>Backend Call #1 success payload — matches the Inventory API's <c>ResolveForPrintResponse</c>.</summary>
    public sealed class ResolveForPrintResponseDto
    {
        public long ProductItemId { get; set; }
        public string MaskedPan { get; set; } = string.Empty;
        public string? HolderName { get; set; }
    }

    /// <summary>Backend Call #2 request body — matches the Inventory API's <c>RecordPrintResultRequest</c>.</summary>
    public sealed class RecordPrintResultRequestDto
    {
        public long BranchId { get; set; }
        public bool Success { get; set; }
        public string? HolderName { get; set; }

        /// <summary>
        /// Generated once per physical print attempt and reused on every retry of this same
        /// attempt (see <c>PrintFlowClient.RecordPrintResultAsync</c>'s retry loop). Not compared
        /// against a persisted table server-side (deliberately lightweight, per the agreed plan) —
        /// carried through for log/audit correlation on both sides.
        /// </summary>
        public string IdempotencyKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Outcome of a Print Flow backend call, distinguishing three cases the caller needs to
    /// react to differently: succeeded; failed with a well-formed business error from the
    /// Inventory API (not worth retrying — the answer won't change); or failed to even get an
    /// answer (network/timeout/5xx — worth retrying, see <see cref="IsTransient"/>).
    /// </summary>
    public sealed record PrintFlowResult<T>(bool Success, T? Data, string? ErrorCode, string? ErrorMessage, bool IsTransient)
    {
        public static PrintFlowResult<T> Ok(T data) => new(true, data, null, null, false);

        public static PrintFlowResult<T> Business(string? code, string? message) => new(false, default, code, message, false);

        public static PrintFlowResult<T> Transient(string? message) => new(false, default, null, message, true);
    }
}
