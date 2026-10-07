using System.Text.Json;
using Microsoft.OpenApi.Any;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// Converts a plain CLR object into the <see cref="IOpenApiAny"/> tree Swashbuckle needs for
    /// <c>OpenApiExample.Value</c>. Example providers (<c>Swagger/Examples/*.cs</c>) author
    /// examples as ordinary anonymous objects/records — the same shapes the real DTOs serialize
    /// to — and this factory does the one-time conversion, so no example provider hand-builds
    /// <see cref="OpenApiObject"/>/<see cref="OpenApiString"/> graphs directly.
    /// <para>
    /// Routes through <c>System.Text.Json</c> with the same casing ASP.NET Core's default
    /// serializer uses (camelCase), so an example's JSON shape matches what a real response body
    /// actually contains.
    /// </para>
    /// </summary>
    internal static class OpenApiAnyFactory
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        /// <summary>Serializes <paramref name="value"/> to JSON and parses it into an <see cref="IOpenApiAny"/>.</summary>
        public static IOpenApiAny From(object? value)
        {
            if (value is null)
            {
                return new OpenApiNull();
            }

            string json = JsonSerializer.Serialize(value, SerializerOptions);
            using JsonDocument document = JsonDocument.Parse(json);
            return Convert(document.RootElement);
        }

        private static IOpenApiAny Convert(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => ConvertArray(element),
            JsonValueKind.String => new OpenApiString(element.GetString()),
            JsonValueKind.Number => ConvertNumber(element),
            JsonValueKind.True => new OpenApiBoolean(true),
            JsonValueKind.False => new OpenApiBoolean(false),
            _ => new OpenApiNull()
        };

        private static IOpenApiAny ConvertObject(JsonElement element)
        {
            var result = new OpenApiObject();
            foreach (JsonProperty property in element.EnumerateObject())
            {
                result[property.Name] = Convert(property.Value);
            }
            return result;
        }

        private static IOpenApiAny ConvertArray(JsonElement element)
        {
            var result = new OpenApiArray();
            foreach (JsonElement item in element.EnumerateArray())
            {
                result.Add(Convert(item));
            }
            return result;
        }

        private static IOpenApiAny ConvertNumber(JsonElement element)
        {
            if (element.TryGetInt64(out long l))
            {
                return new OpenApiLong(l);
            }
            return new OpenApiDouble(element.GetDouble());
        }
    }
}
