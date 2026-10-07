using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>
    /// Error payload returned inside <see cref="ApiResponse{T}"/> when an operation fails.
    /// Mirrors the domain <c>Error</c>. <see cref="MessageArg"/> is a transport-only localization
    /// argument and is never serialized.
    /// </summary>
    public sealed class ApiError
    {
        /// <summary>Stable, machine-readable error identifier (e.g. "Tenant.NotFound").</summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable description. Carries the English default at build time; the localization
        /// filter replaces it in-place with culture-specific text, and leaves this default in place
        /// when no resource entry exists for <see cref="Code"/>.
        /// </summary>
        public string Message { get; internal set; } = string.Empty;

        /// <summary>Error classification (e.g. "Validation", "Conflict", "NotFound").</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Optional localization argument substituted into the resource's <c>{0}</c> placeholder.
        /// Read by the filter; never written to the response body.
        /// </summary>
        [JsonIgnore]
        public string? MessageArg { get; init; }

        /// <summary>
        /// Field-level errors, keyed by field name. Populated only for 422 failures; null otherwise
        /// so it is omitted from the serialized body.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
    }
}