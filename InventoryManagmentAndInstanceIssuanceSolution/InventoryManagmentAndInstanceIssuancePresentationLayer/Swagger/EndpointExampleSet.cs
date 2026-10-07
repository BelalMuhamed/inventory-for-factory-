using System.Collections.Generic;
using System.Linq;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// One named example: <paramref name="Key"/> is the short identifier Swagger UI's dropdown
    /// shows (e.g. <c>"knownWayLine"</c>), <paramref name="Summary"/> is the one-line business
    /// explanation shown next to it, and <paramref name="Value"/> is the example payload — an
    /// ordinary object shaped like the real DTO, converted by <see cref="OpenApiAnyFactory"/>.
    /// </summary>
    public sealed record NamedExample(string Key, string Summary, object Value);

    /// <summary>
    /// All the examples for a single controller action: zero-or-more named request-body examples
    /// (for endpoints with distinct business scenarios), plus zero-or-more named examples per
    /// response status code (success, validation, business-rule failures, etc.).
    /// </summary>
    public sealed class EndpointExampleSet
    {
        /// <summary>Named request-body examples. Empty for endpoints with no body (GET, etc.).</summary>
        public IReadOnlyList<NamedExample> RequestExamples { get; init; } = new List<NamedExample>();

        /// <summary>Named examples per HTTP status code.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<NamedExample>> ResponseExamples { get; init; } =
            new Dictionary<int, IReadOnlyList<NamedExample>>();
    }

    /// <summary>
    /// Fluent builder for <see cref="EndpointExampleSet"/>, used by the per-module files under
    /// <c>Swagger/Examples/</c> so each endpoint's examples read as a short, declarative block
    /// rather than raw dictionary construction.
    /// </summary>
    public sealed class EndpointExampleSetBuilder
    {
        private readonly List<NamedExample> _requestExamples = new();
        private readonly Dictionary<int, List<NamedExample>> _responseExamples = new();

        /// <summary>Adds a named request-body example.</summary>
        public EndpointExampleSetBuilder Request(string key, string summary, object value)
        {
            _requestExamples.Add(new NamedExample(key, summary, value));
            return this;
        }

        /// <summary>Adds a named example for a given HTTP status code.</summary>
        public EndpointExampleSetBuilder Response(int statusCode, string key, string summary, object value)
        {
            if (!_responseExamples.TryGetValue(statusCode, out List<NamedExample>? list))
            {
                list = new List<NamedExample>();
                _responseExamples[statusCode] = list;
            }
            list.Add(new NamedExample(key, summary, value));
            return this;
        }

        /// <summary>Builds the immutable <see cref="EndpointExampleSet"/>.</summary>
        public EndpointExampleSet Build() => new()
        {
            RequestExamples = _requestExamples,
            ResponseExamples = _responseExamples.ToDictionary(
                kvp => kvp.Key, kvp => (IReadOnlyList<NamedExample>)kvp.Value)
        };
    }
}
