// PresentationLayer/Swagger/AcceptLanguageHeaderOperationFilter.cs
using System.Collections.Generic;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// Adds an optional <c>Accept-Language</c> header parameter (en/ar) to every operation so the
    /// response language can be chosen from Swagger UI. The value is consumed by the request
    /// localization middleware (<c>AcceptLanguageHeaderRequestCultureProvider</c>).
    /// </summary>
    public sealed class AcceptLanguageHeaderOperationFilter : IOperationFilter
    {
        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept-Language",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Response language: 'en' (default) or 'ar'.",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Default = new OpenApiString("en"),
                    Enum = new List<IOpenApiAny>
                    {
                        new OpenApiString("en"),
                        new OpenApiString("ar")
                    }
                }
            });
        }
    }
}