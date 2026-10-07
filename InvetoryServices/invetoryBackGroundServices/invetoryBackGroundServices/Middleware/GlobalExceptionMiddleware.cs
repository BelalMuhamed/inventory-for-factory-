using System;
using System.Threading.Tasks;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Machine;
using invetoryBackGroundServices.Resources.Localization;
using MATICA_S3300e.LAN;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace invetoryBackGroundServices.Middleware
{
    /// <summary>
    /// Converts any unhandled exception into the standard <see cref="ApiResponse{T}"/> envelope,
    /// so controllers never need their own try/catch-and-shape-a-response blocks.
    /// <para>
    /// Because this runs outside MVC's filter pipeline, <see cref="Filters.LocalizeErrorResultFilter"/>
    /// never sees these responses - so localization is applied here directly (via
    /// <see cref="ErrorResponseWriter"/>, shared with the JwtBearer auth event handlers in
    /// <see cref="Program"/> for the same reason), against the same resources and the same
    /// culture (already resolved from <c>Accept-Language</c> by the request-localization
    /// middleware upstream). Without this, an unhandled failure would be the one response in the
    /// service that silently ignored the caller's language.
    /// </para>
    /// </summary>
    public sealed class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Logger _log;

        /// <summary>Creates the middleware.</summary>
        public GlobalExceptionMiddleware(RequestDelegate next, Logger log)
        {
            _next = next;
            _log = log;
        }

        /// <summary>Invokes the next middleware, converting any escaping exception.</summary>
        public async Task InvokeAsync(HttpContext context, IStringLocalizer<Messages> localizer)
        {
            try
            {
                await _next(context);
            }
            catch (MachineException ex)
            {
                // Expected, typed hardware failures - already carry the right error/category.
                _log.AppendLog($"{ex.GetType().Name}: {ex.Message}", Logger.LogType.Error);
                await ErrorResponseWriter.WriteAsync(context.Response, ex.Error, localizer);
            }
            catch (Exception ex)
            {
                // Genuinely unexpected: log the detail, return an opaque message. The exception
                // text is never sent to the caller.
                _log.AppendLog($"Unhandled {ex.GetType().Name}: {ex}", Logger.LogType.Error);
                await ErrorResponseWriter.WriteAsync(context.Response, MachineErrors.Internal(), localizer);
            }
        }
    }
}

