using System;
using System.Threading.Tasks;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Middleware
{
    /// <summary>
    /// Terminal safety net for the request pipeline. Business outcomes flow through
    /// <c>Result</c> and never reach here; this middleware exists for truly unhandled
    /// exceptions. It maps known infrastructure exceptions to their proper status
    /// (e.g. <see cref="DbUpdateConcurrencyException"/> → 409, per the ROWVERSION rule),
    /// logs full detail server-side with the request's correlation id, and returns the
    /// standard <see cref="ApiResponse{T}"/> envelope. Exception internals (stack traces,
    /// connection strings, decryption keys) are never written to the response body.
    /// </summary>
    public sealed class GlobalExceptionMiddleware
    {
        /// <summary>Response header carrying the correlation id back to the caller.</summary>
        public const string CorrelationHeader = "X-Trace-Id";

        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        /// <summary>Creates the middleware.</summary>
        /// <param name="next">The next delegate in the pipeline.</param>
        /// <param name="logger">Structured logger.</param>
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>Invokes the middleware, guarding the downstream pipeline.</summary>
        /// <param name="context">The current HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            // Surface the correlation id on every response, before the body is written.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationHeader] = context.TraceIdentifier;
                return Task.CompletedTask;
            });

            try
            {
                await _next(context);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict on {Method} {Path}. TraceId: {TraceId}",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);

                await WriteEnvelopeAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    new ApiError
                    {
                        Code = "Concurrency.Conflict",
                        Message = "The resource was modified by another operation. Reload and try again.",
                        Category = "Conflict"
                    });
            }
            catch (Exception ex)
            {
                // Log the full exception server-side; return an opaque message to the client.
                _logger.LogError(
                    ex,
                    "Unhandled exception on {Method} {Path}. TraceId: {TraceId}",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);

                await WriteEnvelopeAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    new ApiError
                    {
                        Code = "Server.Error",
                        Message = "An unexpected error occurred. Reference the trace id when reporting this.",
                        Category = "ServerError"
                    });
            }
        }

        private static async Task WriteEnvelopeAsync(HttpContext context, int statusCode, ApiError error)
        {
            // If the response has already started, the pipeline is committed — we cannot rewrite it.
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var body = ApiResponse<object>.Fail(error, context.TraceIdentifier);
            await context.Response.WriteAsJsonAsync(body);
        }
    }
}
