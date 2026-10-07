using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// Attaches the named request/response examples registered in <see cref="ExampleCatalog"/> to
    /// each operation. An action with no catalog entry is left exactly as Swashbuckle generated it
    /// — this filter is purely additive and never removes or alters schema-derived content.
    /// </summary>
    public sealed class ExamplesOperationFilter : IOperationFilter
    {
        private const string JsonMediaType = "application/json";

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var key = new EndpointKey(context.MethodInfo.DeclaringType?.Name ?? string.Empty, context.MethodInfo.Name);
            if (!ExampleCatalog.All.TryGetValue(key, out EndpointExampleSet? examples))
            {
                return;
            }

            ApplyRequestExamples(operation, examples);
            ApplyResponseExamples(operation, examples);
        }

        private static void ApplyRequestExamples(OpenApiOperation operation, EndpointExampleSet examples)
        {
            if (examples.RequestExamples.Count == 0 || operation.RequestBody is null)
            {
                return;
            }

            OpenApiMediaType? mediaType = ResolveMediaType(operation.RequestBody.Content);
            if (mediaType is null)
            {
                return;
            }

            foreach (NamedExample example in examples.RequestExamples)
            {
                mediaType.Examples[example.Key] = new OpenApiExample
                {
                    Summary = example.Summary,
                    Value = OpenApiAnyFactory.From(example.Value)
                };
            }
        }

        private static void ApplyResponseExamples(OpenApiOperation operation, EndpointExampleSet examples)
        {
            foreach ((int statusCode, var namedExamples) in examples.ResponseExamples)
            {
                string statusKey = statusCode.ToString();
                if (!operation.Responses.TryGetValue(statusKey, out OpenApiResponse? response))
                {
                    continue;
                }

                OpenApiMediaType? mediaType = ResolveMediaType(response.Content);
                if (mediaType is null)
                {
                    // A response with no declared content (e.g. a 401/403 the auth middleware
                    // returns with an empty body, before the action or its ApiResponse<T>
                    // envelope is ever involved) legitimately has nothing to attach an example to.
                    continue;
                }

                foreach (NamedExample example in namedExamples)
                {
                    mediaType.Examples[example.Key] = new OpenApiExample
                    {
                        Summary = example.Summary,
                        Value = OpenApiAnyFactory.From(example.Value)
                    };
                }
            }
        }

        private static OpenApiMediaType? ResolveMediaType(System.Collections.Generic.IDictionary<string, OpenApiMediaType> content)
        {
            if (content.TryGetValue(JsonMediaType, out OpenApiMediaType? exact))
            {
                return exact;
            }

            return content.Values.FirstOrDefault();
        }
    }
}
