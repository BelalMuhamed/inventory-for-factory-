using DomainLayer.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>
    /// Maps domain <see cref="Result"/> / <see cref="Result{T}"/> outcomes to HTTP responses,
    /// wrapping every payload in <see cref="ApiResponse{T}"/>. Status codes are driven solely by
    /// <see cref="ErrorCategory"/> (API Spec §2.5) — no message-text inspection — so the mapping
    /// is deterministic and lives in exactly one place.
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Converts a value-bearing <see cref="Result{T}"/> to an <see cref="IActionResult"/>.
        /// On success returns 200 with the value; on failure returns the category's status code
        /// with an error envelope.
        /// </summary>
        /// <typeparam name="T">Type of the success value.</typeparam>
        /// <param name="result">The operation outcome.</param>
        /// <param name="controller">The calling controller, used to resolve the trace ID.</param>
        public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
        {
            string traceId = ResolveTraceId(controller.HttpContext);

            return result.IsSuccess
                ? new OkObjectResult(ApiResponse<T>.Ok(result.Value, traceId))
                : BuildFailure<T>(result.Error, traceId);
        }

        /// <summary>
        /// Converts a valueless <see cref="Result"/> to an <see cref="IActionResult"/>.
        /// On success returns 200 with a null payload; on failure returns the category's status code.
        /// </summary>
        /// <param name="result">The operation outcome.</param>
        /// <param name="controller">The calling controller, used to resolve the trace ID.</param>
        public static IActionResult ToActionResult(this Result result, ControllerBase controller)
        {
            string traceId = ResolveTraceId(controller.HttpContext);

            return result.IsSuccess
                ? new OkObjectResult(ApiResponse<object>.Ok(null!, traceId))
                : BuildFailure<object>(result.Error, traceId);
        }

        private static IActionResult BuildFailure<T>(Error error, string traceId)
        {
            var body = ApiResponse<T>.Fail(
                new ApiError
                {
                    Code = error.Code,
                    Message = error.Message,        // English default → fallback when no resource key exists
                    Category = error.Category.ToString(),
                    MessageArg = error.MessageArg,  // substituted into the resource's {0} placeholder
                    ValidationErrors = error.Details // field-level detail; null for most errors, then omitted
                },
                traceId);

            return new ObjectResult(body) { StatusCode = MapStatusCode(error.Category) };
        }

        /// <summary>Maps an <see cref="ErrorCategory"/> to its HTTP status code (API Spec §2.5).</summary>
        private static int MapStatusCode(ErrorCategory category) => category switch
        {
            ErrorCategory.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorCategory.Conflict => StatusCodes.Status409Conflict,
            ErrorCategory.NotFound => StatusCodes.Status404NotFound,
            ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCategory.Internal => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        private static string ResolveTraceId(HttpContext? context) =>
            context?.TraceIdentifier ?? string.Empty;
    }
}
