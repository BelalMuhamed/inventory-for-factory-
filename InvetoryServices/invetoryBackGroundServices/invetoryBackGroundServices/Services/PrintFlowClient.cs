using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.PrintFlow;
using MATICA_S3300e.LAN;

namespace invetoryBackGroundServices.Services
{
    /// <summary>
    /// Default <see cref="IPrintFlowClient"/> implementation. Uses a typed <see cref="HttpClient"/>
    /// (registered via <c>AddHttpClient&lt;IPrintFlowClient, PrintFlowClient&gt;</c> in
    /// <c>Program.cs</c>, base address bound from <see cref="Options.InventoryApiOptions"/>) rather
    /// than constructing a new <see cref="HttpClient"/> per call — the old <c>API_HttpClient</c>
    /// this replaces did exactly that, which is the socket-exhaustion anti-pattern flagged in the
    /// prior review. TLS certificate validation is left at its default (real, validated) here,
    /// unlike the LAN calls to the physical printer — this client only ever talks to the real
    /// Inventory API.
    /// </summary>
    public sealed class PrintFlowClient : IPrintFlowClient
    {
        private const int MaxRecordResultAttempts = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly Logger _log;

        public PrintFlowClient(HttpClient httpClient, Logger log)
        {
            _httpClient = httpClient;
            _log = log;
        }

        /// <inheritdoc />
        public async Task<PrintFlowResult<ResolveForPrintResponseDto>> ResolveForPrintAsync(
            string bearerToken, string pan, long productId, long branchId, CancellationToken cancellationToken)
        {
            var request = new ResolveForPrintRequestDto { Pan = pan, ProductId = productId, BranchId = branchId };

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/print-flow/resolve-for-print")
                {
                    Content = JsonContent.Create(request)
                };
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                return await ParseAsync<ResolveForPrintResponseDto>(response, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.AppendLog("resolve-for-print call failed (transient): " + ex.Message, Logger.LogType.Error);
                return PrintFlowResult<ResolveForPrintResponseDto>.Transient(ex.Message);
            }
        }

        /// <inheritdoc />
        public async Task<PrintFlowResult<object?>> RecordPrintResultAsync(
            string bearerToken, long productItemId, long branchId, bool success, string? holderName,
            string idempotencyKey, CancellationToken cancellationToken)
        {
            var request = new RecordPrintResultRequestDto
            {
                BranchId = branchId,
                Success = success,
                HolderName = holderName,
                IdempotencyKey = idempotencyKey
            };

            PrintFlowResult<object?> lastResult = PrintFlowResult<object?>.Transient("not attempted");

            for (int attempt = 1; attempt <= MaxRecordResultAttempts; attempt++)
            {
                try
                {
                    using var httpRequest = new HttpRequestMessage(
                        HttpMethod.Post, $"api/print-flow/{productItemId}/print-result")
                    {
                        Content = JsonContent.Create(request)
                    };
                    httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                    using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                    lastResult = await ParseAsync<object?>(response, cancellationToken);

                    // Only a transient failure (network/timeout/5xx) is worth retrying — a
                    // well-formed business failure (4xx) will return the exact same answer on
                    // retry, so resending it just delays telling the caller what actually happened.
                    if (!lastResult.IsTransient)
                    {
                        return lastResult;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    lastResult = PrintFlowResult<object?>.Transient(ex.Message);
                }

                _log.AppendLog(
                    $"print-result call attempt {attempt}/{MaxRecordResultAttempts} for item {productItemId}, " +
                    $"idempotencyKey {idempotencyKey}: {lastResult.ErrorMessage}",
                    Logger.LogType.Error);

                if (attempt < MaxRecordResultAttempts)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }

            // Every attempt was transient: the physical outcome is known but could not be
            // confirmed with the Inventory API. The caller must surface this distinctly rather
            // than treat it as an ordinary failure — see the plan's "printer succeeds, backend
            // logging fails" scenario.
            return lastResult;
        }

        private static async Task<PrintFlowResult<T>> ParseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            // A 5xx (or an empty/unparseable body on any status) is treated as transient — the
            // server didn't give us a real business answer to act on.
            if ((int)response.StatusCode >= 500 || string.IsNullOrWhiteSpace(body))
            {
                return PrintFlowResult<T>.Transient($"HTTP {(int)response.StatusCode}");
            }

            ApiEnvelope<T>? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return PrintFlowResult<T>.Transient($"HTTP {(int)response.StatusCode} with an unparseable body");
            }

            if (envelope is null)
            {
                return PrintFlowResult<T>.Transient($"HTTP {(int)response.StatusCode} with an empty body");
            }

            if (envelope.Success)
            {
                return PrintFlowResult<T>.Ok(envelope.Data!);
            }

            // A well-formed 4xx business rejection (wrong branch, card not found, insufficient
            // stock, already disposed, ...) — not transient, never retried.
            return PrintFlowResult<T>.Business(envelope.Error?.Code, envelope.Error?.Message);
        }
    }
}
