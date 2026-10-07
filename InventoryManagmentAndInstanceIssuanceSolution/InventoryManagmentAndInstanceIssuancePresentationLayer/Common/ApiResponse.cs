namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>
    /// Uniform response envelope returned by every endpoint, on both success and failure.
    /// Clients branch on <see cref="Success"/>: when true, <see cref="Data"/> holds the payload
    /// and <see cref="Error"/> is null; when false, <see cref="Error"/> describes the failure and
    /// <see cref="Data"/> is null. <see cref="TraceId"/> correlates client reports with logs.
    /// Implements <see cref="ILocalizableApiResponse"/> so the central error-localization filter
    /// can replace the message without knowing the payload type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of the success payload.</typeparam>
    public sealed class ApiResponse<T> : ILocalizableApiResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>The payload on success; null on failure.</summary>
        public T? Data { get; init; }

        /// <summary>The failure detail on error; null on success.</summary>
        public ApiError? Error { get; init; }

       

        /// <summary>Builds a successful envelope wrapping <paramref name="data"/>.</summary>
        public static ApiResponse<T> Ok(T data, string traceId) => new()
        {
            Success = true,
            Data = data,
            Error = null,
          
        };

        /// <summary>Builds a failed envelope carrying <paramref name="error"/>.</summary>
        public static ApiResponse<T> Fail(ApiError error, string traceId) => new()
        {
            Success = false,
            Data = default,
            Error = error,
          
        };

        /// <inheritdoc />
        public void ReplaceErrorMessage(string localizedMessage)
        {
            if (Error is not null)
            {
                Error.Message = localizedMessage;
            }
        }
    }
}