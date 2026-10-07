using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace invetoryBackGroundServices.Common
{
    /// <summary>
    /// Uniform response envelope returned by every endpoint, on both success and failure.
    /// <para>
    /// Deliberately mirrors the Inventory API's <c>ApiResponse&lt;T&gt;</c> wire shape exactly
    /// (<c>Success</c>/<c>Data</c>/<c>Error</c>, with <c>Code</c>/<c>Message</c>/<c>Category</c>/
    /// <c>ValidationErrors</c> inside the error) so a single frontend error-handling path works
    /// against both services. The one intentional deviation: the Inventory API's <c>Ok</c>/
    /// <c>Fail</c> factories take a <c>traceId</c> parameter that is accepted and then never
    /// assigned to anything (its backing property appears to have been removed). That vestigial
    /// parameter is not replicated here - the serialized shape is identical either way, and
    /// copying a dead parameter into a new codebase just to match a signature would be
    /// propagating a defect, not matching a contract.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Type of the success payload.</typeparam>
    public sealed class ApiResponse<T> : ILocalizableApiResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>The payload on success; null on failure.</summary>
        public T? Data { get; init; }

        /// <summary>The failure detail on error; null on success.</summary>
        public ApiError? Error { get; init; }

        /// <summary>Builds a successful envelope wrapping <paramref name="data"/>.</summary>
        public static ApiResponse<T> Ok(T data) => new()
        {
            Success = true,
            Data = data,
            Error = null
        };

        /// <summary>Builds a failed envelope carrying <paramref name="error"/>.</summary>
        public static ApiResponse<T> Fail(ApiError error) => new()
        {
            Success = false,
            Data = default,
            Error = error
        };

        /// <inheritdoc />
        public void ReplaceErrorMessage(string localizedMessage)
        {
            if (Error is not null)
            {
                Error.Message = localizedMessage;
            }
        }
    }

    /// <summary>
    /// Error payload returned inside <see cref="ApiResponse{T}"/> when an operation fails.
    /// Same shape as the Inventory API's <c>ApiError</c>. <see cref="Category"/> is a string on
    /// the wire in both services, which is what lets this service use hardware-specific
    /// categories (Timeout, CommunicationFailure, MachineRejected, ProtocolError) without
    /// diverging from the shared contract.
    /// </summary>
    public sealed class ApiError
    {
        /// <summary>Stable, machine-readable error identifier (e.g. "Machine.Timeout").</summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable description. Carries the English default at build time; the localization
        /// filter replaces it in-place with culture-specific text, and leaves this default when no
        /// resource entry exists for <see cref="Code"/>.
        /// </summary>
        public string Message { get; internal set; } = string.Empty;

        /// <summary>Error classification (see <see cref="MachineErrorCategory"/>).</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Optional localization argument substituted into the resource's <c>{0}</c> placeholder.
        /// Read by the filter; never written to the response body.
        /// </summary>
        [JsonIgnore]
        public string? MessageArg { get; init; }

        /// <summary>
        /// Optional field-level errors, keyed by field name. Populated only for 422 failures;
        /// null otherwise so it is omitted from the serialized body.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

        /// <summary>
        /// Appends <paramref name="detail"/> to <paramref name="baseMessage"/> when present,
        /// otherwise returns <paramref name="baseMessage"/> unchanged. The single place this
        /// composition happens, used identically by <see cref="MachineError.ToApiError"/> (the
        /// English default) and by the localization filter/middleware (the culture-specific
        /// text) - see <see cref="MachineError.MessageArg"/>'s doc comment for why this is a
        /// plain append rather than a resx <c>{0}</c> substitution.
        /// </summary>
        public static string ComposeMessage(string baseMessage, string? detail) =>
            string.IsNullOrWhiteSpace(detail) ? baseMessage : $"{baseMessage} {detail}";
    }

    /// <summary>
    /// Lets the culture-aware filter read an error's code/arg and replace its message on an
    /// <see cref="ApiResponse{T}"/> without knowing the generic payload type. Same mechanism as
    /// the Inventory API's interface of the same name.
    /// </summary>
    public interface ILocalizableApiResponse
    {
        /// <summary>The error payload, or null on success responses.</summary>
        ApiError? Error { get; }

        /// <summary>Replaces the error message with its localized text.</summary>
        void ReplaceErrorMessage(string localizedMessage);
    }
}
