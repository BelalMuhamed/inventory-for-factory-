namespace invetoryBackGroundServices.Common
{
    /// <summary>
    /// Classifies a failure so the presentation layer can map it to an HTTP status code without
    /// inspecting message text - same role as the Inventory API's <c>ErrorCategory</c>, but with
    /// the hardware-communication cases a CRUD API doesn't have. Serialized as its string name
    /// into <see cref="ApiError.Category"/>.
    /// </summary>
    public static class MachineErrorCategory
    {
        /// <summary>Request data violates a rule. Maps to HTTP 422.</summary>
        public const string Validation = "Validation";

        /// <summary>Caller is not authenticated. Maps to HTTP 401.</summary>
        public const string Unauthorized = "Unauthorized";

        /// <summary>Authenticated but out of scope for this token. Maps to HTTP 403.</summary>
        public const string Forbidden = "Forbidden";

        /// <summary>Could not reach the machine at all (refused, unreachable, TLS failure). Maps to HTTP 502.</summary>
        public const string CommunicationFailure = "CommunicationFailure";

        /// <summary>Reached the machine but it did not respond in time. Maps to HTTP 504.</summary>
        public const string Timeout = "Timeout";

        /// <summary>Machine responded but reported an error or refused the operation. Maps to HTTP 409.</summary>
        public const string MachineRejected = "MachineRejected";

        /// <summary>Machine responded, but the response could not be parsed as expected. Maps to HTTP 502.</summary>
        public const string ProtocolError = "ProtocolError";

        /// <summary>
        /// The Inventory API (not the physical machine) rejected a print-flow backend call for a
        /// business reason - wrong branch, card not found, insufficient stock, already disposed.
        /// Maps to HTTP 409. Kept distinct from <see cref="MachineRejected"/> since these two
        /// "somethings" (the physical device vs. the Inventory API) call for different operator
        /// responses even though both map to the same status code.
        /// </summary>
        public const string BackendRejected = "BackendRejected";

        /// <summary>
        /// Could not reach, or could not get a usable answer from, the Inventory API for a
        /// print-flow backend call, after retries. Maps to HTTP 502.
        /// </summary>
        public const string BackendUnavailable = "BackendUnavailable";

        /// <summary>Anything else, including bugs. Maps to HTTP 500.</summary>
        public const string Internal = "Internal";
    }

    /// <summary>
    /// An operation failure, carrying everything needed to build an <see cref="ApiError"/> and
    /// pick an HTTP status. Kept separate from <see cref="ApiError"/> so the machine/application
    /// layers never depend on presentation types.
    /// </summary>
    /// <param name="Code">Stable identifier, also the localization resource key.</param>
    /// <param name="Category">One of <see cref="MachineErrorCategory"/>.</param>
    /// <param name="Message">English default message.</param>
    /// <param name="MessageArg">
    /// Optional contextual detail (e.g. the Inventory API's own business-rejection reason, or an
    /// exception message). Appended to the base message by <see cref="ToApiError"/> and by the
    /// localization filter/middleware - not substituted via a resx <c>{0}</c> placeholder. That
    /// distinction matters: a placeholder-based approach silently drops this detail whenever
    /// <see cref="MessageArg"/> is null, because <c>IStringLocalizer</c>'s no-args indexer
    /// returns the resource text completely unformatted rather than filling the placeholder with
    /// anything - which is exactly how this went wrong the first time (every resx entry in this
    /// catalogue was missing <c>{0}</c> entirely, so the detail was computed, passed, and then
    /// silently discarded at every call site that supplied one).
    /// </param>
    public readonly record struct MachineError(string Code, string Category, string Message, string? MessageArg = null)
    {
        /// <summary>HTTP status this error's category maps to.</summary>
        public int StatusCode => Category switch
        {
            MachineErrorCategory.Validation => 422,
            MachineErrorCategory.Unauthorized => 401,
            MachineErrorCategory.Forbidden => 403,
            MachineErrorCategory.MachineRejected => 409,
            MachineErrorCategory.Timeout => 504,
            MachineErrorCategory.CommunicationFailure => 502,
            MachineErrorCategory.ProtocolError => 502,
            MachineErrorCategory.BackendRejected => 409,
            MachineErrorCategory.BackendUnavailable => 502,
            _ => 500
        };

        /// <summary>
        /// Projects this error onto the wire contract, with <see cref="MessageArg"/> (if present)
        /// appended to <see cref="Message"/> - see <see cref="MessageArg"/>'s own doc comment for
        /// why appending rather than substituting a placeholder.
        /// </summary>
        public ApiError ToApiError() => new()
        {
            Code = Code,
            Category = Category,
            Message = ApiError.ComposeMessage(Message, MessageArg),
            MessageArg = MessageArg
        };
    }

    /// <summary>
    /// Catalogue of every failure this service can report. Codes double as localization resource
    /// keys (see <c>Resources/Localization/Messages.resx</c> and its <c>.ar</c> counterpart), so
    /// adding an entry here means adding a matching key to both resource files.
    /// <para>
    /// Messages deliberately say what happened in operator terms ("the machine did not respond
    /// within N seconds") rather than surfacing raw exception text or raw machine JSON.
    /// </para>
    /// </summary>
    public static class MachineErrors
    {
        /// <summary>The supplied IP address is missing or not a valid address.</summary>
        public static MachineError InvalidIp() => new(
            "Machine.InvalidIp", MachineErrorCategory.Validation,
            "The supplied machine IP address is missing or invalid.");

        /// <summary>The supplied port is missing or not a valid TCP port.</summary>
        public static MachineError InvalidPort() => new(
            "Machine.InvalidPort", MachineErrorCategory.Validation,
            "The supplied machine port is missing or invalid.");

        /// <summary>Could not open a connection to the machine at all.</summary>
        public static MachineError ConnectionFailed(string? detail = null) => new(
            "Machine.ConnectionFailed", MachineErrorCategory.CommunicationFailure,
            "Failed to connect to the machine. Check that it is powered on and reachable on the network.",
            detail);

        /// <summary>The machine accepted the connection but did not answer in time.</summary>
        public static MachineError Timeout(string? detail = null) => new(
            "Machine.Timeout", MachineErrorCategory.Timeout,
            "The machine did not respond within the configured timeout.",
            detail);

        /// <summary>The machine answered, but reported an error for the requested command.</summary>
        public static MachineError Rejected(string? detail = null) => new(
            "Machine.Rejected", MachineErrorCategory.MachineRejected,
            "The machine rejected the requested operation.",
            detail);

        /// <summary>The machine is not in a state where the operation can proceed.</summary>
        public static MachineError NotReady(string? detail = null) => new(
            "Machine.NotReady", MachineErrorCategory.MachineRejected,
            "The machine is not ready to perform this operation.",
            detail);

        /// <summary>The machine answered with something that could not be parsed.</summary>
        public static MachineError UnexpectedResponse(string? detail = null) => new(
            "Machine.UnexpectedResponse", MachineErrorCategory.ProtocolError,
            "The machine returned an unexpected or unreadable response.",
            detail);

        /// <summary>An unhandled failure inside this service.</summary>
        public static MachineError Internal() => new(
            "Machine.InternalError", MachineErrorCategory.Internal,
            "An internal error occurred in the printer agent service.");

        /// <summary>The request's branch does not match the Print Agent token's own scope.</summary>
        public static MachineError BranchScopeMismatch() => new(
            "Machine.BranchScopeMismatch", MachineErrorCategory.Forbidden,
            "The requested branch does not match this token's own scope.");

        /// <summary>The machine is not ready and could not eject after a failed step (worse than the original failure).</summary>
        public static MachineError CannotEject() => new(
            "Machine.CannotEject", MachineErrorCategory.MachineRejected,
            "The machine could not eject the card.");

        /// <summary>Backend Call #1/#2's Inventory API call returned a well-formed business rejection.</summary>
        public static MachineError BackendRejected(string? detail = null) => new(
            "Machine.BackendRejected", MachineErrorCategory.BackendRejected,
            "The Inventory API rejected this print request.", detail);

        /// <summary>Backend Call #1/#2 could not reach, or get a usable answer from, the Inventory API.</summary>
        public static MachineError BackendUnavailable(string? detail = null) => new(
            "Machine.BackendUnavailable", MachineErrorCategory.BackendUnavailable,
            "Could not reach the Inventory API to validate or record this print.", detail);

        /// <summary>
        /// No Print Agent token was supplied, or the supplied one is missing, malformed, expired,
        /// or signed with the wrong key. Used by the JwtBearer <c>OnChallenge</c> handler in
        /// <see cref="Program"/> - authentication failure never reaches a controller action, so
        /// nothing in this catalogue was ever returning a body for this case before.
        /// </summary>
        public static MachineError Unauthenticated() => new(
            "Machine.Unauthenticated", MachineErrorCategory.Unauthorized,
            "A valid Print Agent token is required.");

        /// <summary>
        /// A token was supplied and validated, but it does not satisfy the <c>PrintAgentOnly</c>
        /// policy (missing the <c>purpose</c> claim - i.e. a token that is structurally valid but
        /// was never meant for this service). Used by the JwtBearer <c>OnForbidden</c> handler.
        /// </summary>
        public static MachineError Forbidden() => new(
            "Machine.Forbidden", MachineErrorCategory.Forbidden,
            "This token is not permitted to perform this operation.");
    }
}
