using System.Collections.Generic;

namespace DomainLayer.Common
{
    /// <summary>
    /// Immutable description of a single failure. Carries a stable machine-readable
    /// <see cref="Code"/> (for clients and logs), a human-readable <see cref="Message"/>,
    /// and a <see cref="Category"/> that determines HTTP status mapping.
    /// </summary>
    public sealed record Error
    {
        /// <summary>Sentinel value representing the absence of an error (used by successful results).</summary>
        public static readonly Error None = new(string.Empty, string.Empty, ErrorCategory.None);

        /// <summary>
        /// Creates an error.
        /// </summary>
        /// <param name="code">Stable, machine-readable identifier (e.g. "User.NotFound", "Tenant.DuplicateIdentifier").</param>
        /// <param name="message">Human-readable description of what went wrong.</param>
        /// <param name="category">Classification driving the HTTP status code.</param>
        public Error(string code, string message, ErrorCategory category)
        {
            Code = code;
            Message = message;
            Category = category;
        }

        /// <summary>Stable, machine-readable error identifier.</summary>
        public string Code { get; }

        /// <summary>Human-readable error description.</summary>
        public string Message { get; }

        /// <summary>Classification used to map this error to an HTTP status code.</summary>
        public ErrorCategory Category { get; }
        // DomainLayer/Common/Error.cs — add an optional argument carried alongside the error
        /// <summary>Optional value substituted into the localized message's {0} placeholder.</summary>
        public string? MessageArg { get; private init; }

        /// <summary>Returns a copy of this error tagged with a message argument for localization.</summary>
        public Error WithArg(string arg) => this with { MessageArg = arg };

        /// <summary>
        /// Optional field-level detail, keyed by field or element path (e.g. <c>"cards[3]"</c>).
        /// Surfaced by the presentation layer as <c>ApiError.ValidationErrors</c> — the same shape
        /// model-state failures already produce, so clients parse one structure rather than two.
        /// Null for the great majority of errors, and omitted from the serialized body when null.
        /// <para>
        /// Added in Card File Generation, Phase 9.2, because a rejected generation request has to
        /// tell the caller <em>which</em> cards failed and why, and the success payload is null on
        /// a failed <see cref="Result{TValue}"/> by design.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Details { get; private init; }

        /// <summary>Returns a copy of this error carrying field-level detail.</summary>
        /// <param name="details">Failure detail keyed by field or element path.</param>
        public Error WithDetails(IReadOnlyDictionary<string, string[]> details) => this with { Details = details };

        /// <summary>Creates a <see cref="ErrorCategory.Validation"/> error (→ HTTP 422).</summary>
        public static Error Validation(string code, string message) => new(code, message, ErrorCategory.Validation);

        /// <summary>Creates a <see cref="ErrorCategory.Conflict"/> error (→ HTTP 409).</summary>
        public static Error Conflict(string code, string message) => new(code, message, ErrorCategory.Conflict);

        /// <summary>Creates a <see cref="ErrorCategory.NotFound"/> error (→ HTTP 404).</summary>
        public static Error NotFound(string code, string message) => new(code, message, ErrorCategory.NotFound);

        /// <summary>Creates an <see cref="ErrorCategory.Unauthorized"/> error (→ HTTP 401).</summary>
        public static Error Unauthorized(string code, string message) => new(code, message, ErrorCategory.Unauthorized);

        /// <summary>Creates a <see cref="ErrorCategory.Forbidden"/> error (→ HTTP 403).</summary>
        public static Error Forbidden(string code, string message) => new(code, message, ErrorCategory.Forbidden);

        /// <summary>Creates an <see cref="ErrorCategory.Internal"/> error (→ HTTP 500).</summary>
        public static Error Internal(string code, string message) => new(code, message, ErrorCategory.Internal);
    }
}
