using System.Linq;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>
    /// Replaces the default <c>[ApiController]</c> model-validation response (HTTP 400 with
    /// <c>ValidationProblemDetails</c>) with the platform's standard envelope: HTTP 422 carrying
    /// an <see cref="ApiError"/> whose <see cref="ApiError.ValidationErrors"/> lists the offending
    /// fields. Wired once via <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>, so no
    /// controller writes validation-handling code (API Spec §2.5: 422 = request body fails rules).
    /// </summary>
    public static class ValidationResponseFactory
    {
        /// <summary>
        /// Builds the 422 result from the current <see cref="ModelStateDictionary"/>.
        /// Assigned to <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c> at startup.
        /// </summary>
        /// <param name="context">The action context whose <c>ModelState</c> is invalid.</param>
        public static IActionResult Build(ActionContext context)
        {
            var fieldErrors = context.ModelState
                .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(modelError => modelError.ErrorMessage)
                        .ToArray());

            var body = ApiResponse<object>.Fail(
                new ApiError
                {
                    Code = "Validation.Failed",
                    Message = "One or more validation errors occurred.",
                    Category = "Validation",
                    ValidationErrors = fieldErrors
                },
                context.HttpContext.TraceIdentifier);

            return new ObjectResult(body)
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity
            };
        }
    }
}
