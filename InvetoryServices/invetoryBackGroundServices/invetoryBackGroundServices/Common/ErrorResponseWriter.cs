using System.Text.Json;
using System.Threading.Tasks;
using invetoryBackGroundServices.Resources.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace invetoryBackGroundServices.Common
{
    /// <summary>
    /// Writes a localized <see cref="ApiResponse{T}"/> failure directly to an
    /// <see cref="HttpResponse"/>, bypassing MVC entirely. Needed in two places that sit outside
    /// the controller/filter pipeline and so can never go through
    /// <see cref="Filters.LocalizeErrorResultFilter"/>: <c>GlobalExceptionMiddleware</c> (runs
    /// before MVC even starts) and the JwtBearer <c>OnChallenge</c>/<c>OnForbidden</c> events in
    /// <see cref="Program"/> (authentication/authorization failures never reach a controller
    /// action at all). Both used to duplicate this logic; kept in one place now so a future fix
    /// only needs to happen once.
    /// </summary>
    public static class ErrorResponseWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Localizes <paramref name="error"/>'s base message by its code, appends
        /// <see cref="MachineError.MessageArg"/> if present (see that property's doc comment for
        /// why this is an append rather than a resx placeholder substitution), and writes the
        /// resulting <see cref="ApiResponse{T}"/> to <paramref name="response"/> with the status
        /// code the error's category maps to.
        /// </summary>
        public static async Task WriteAsync(
            HttpResponse response, MachineError error, IStringLocalizer<Messages> localizer)
        {
            if (response.HasStarted)
            {
                return;
            }

            ApiError apiError = error.ToApiError();

            LocalizedString localized = localizer[apiError.Code];
            if (!localized.ResourceNotFound)
            {
                apiError.Message = ApiError.ComposeMessage(localized.Value, error.MessageArg);
            }

            response.Clear();
            response.StatusCode = error.StatusCode;
            response.ContentType = "application/json";

            await response.WriteAsync(JsonSerializer.Serialize(ApiResponse<object>.Fail(apiError), JsonOptions));
        }
    }
}
