using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// Documents every available enum value directly on its Swagger schema. Every persisted enum
    /// in this codebase serializes over the wire as its numeric value (no
    /// <c>JsonStringEnumConverter</c> is registered — see Docs/PROJECT_KNOWLEDGE.md §12, enum
    /// persistence), and Swashbuckle's own XML-comment support annotates the enum <em>type</em>
    /// (from <c>IncludeXmlComments</c>) but not each individual member. This filter closes that
    /// gap by reading the same XML doc comments already on every enum member
    /// (<c>DomainLayer/Enums/*.cs</c>) via <see cref="XmlDocIndex"/> and listing "value = Name:
    /// summary" for each one, so a consumer never has to open the source to know what <c>2</c>
    /// means on a given field.
    /// <para>
    /// Runs after Swashbuckle's built-in XML-comments schema filter (registration order in
    /// <c>Program.cs</c>), so it appends to — rather than overwrites — the enum type's own
    /// <c>&lt;summary&gt;</c>, which that filter has already placed in <see cref="OpenApiSchema.Description"/>.
    /// </para>
    /// </summary>
    public sealed class EnumSchemaDescriptionsFilter : ISchemaFilter
    {
        /// <inheritdoc />
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            Type type = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
            if (!type.IsEnum)
            {
                return;
            }

            IReadOnlyList<string> valueLines = DescribeMembers(type);
            if (valueLines.Count == 0)
            {
                return;
            }

            string enumDescription = "Values: " + string.Join("; ", valueLines) + ".";
            schema.Description = string.IsNullOrWhiteSpace(schema.Description)
                ? enumDescription
                : $"{schema.Description} {enumDescription}";

            // Codegen-tool convenience (NSwag/OpenAPI-Generator convention): a parallel array of
            // member names alongside the numeric x-enum values Swashbuckle already emits, so
            // generated clients can produce a named enum instead of a bare int.
            schema.Extensions["x-enumNames"] = BuildNameArray(type);
        }

        private static IReadOnlyList<string> DescribeMembers(Type enumType)
        {
            var lines = new List<string>();
            foreach (string name in Enum.GetNames(enumType))
            {
                object rawValue = Enum.Parse(enumType, name);
                long numericValue = System.Convert.ToInt64(rawValue);
                string memberId = $"F:{enumType.FullName}.{name}";
                string? summary = XmlDocIndex.Instance.GetSummary(memberId);

                lines.Add(summary is null
                    ? $"{numericValue} = {name}"
                    : $"{numericValue} = {name} ({summary})");
            }
            return lines;
        }

        private static OpenApiArray BuildNameArray(Type enumType)
        {
            var array = new OpenApiArray();
            foreach (string name in Enum.GetNames(enumType).OrderBy(n => System.Convert.ToInt64(Enum.Parse(enumType, n))))
            {
                array.Add(new OpenApiString(name));
            }
            return array;
        }
    }
}
