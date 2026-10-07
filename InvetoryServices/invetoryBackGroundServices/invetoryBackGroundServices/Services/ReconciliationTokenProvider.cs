using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Options;
using invetoryBackGroundServices.PrintFlow;
using MATICA_S3300e.LAN;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Services
{
    /// <summary>
    /// Exchanges the Printer Agent's own standing <see cref="ReconciliationCredentialOptions"/>
    /// for a fresh, short-lived access token via the Inventory API's
    /// <c>POST api/auth/service-token</c>. Called once per <c>OutboxReconciliationJob</c> run -
    /// there is no caching here, deliberately: the job runs at most every 30 minutes (plus once at
    /// startup), so there is no meaningful cost to minting fresh every time, and no cache-
    /// invalidation/near-expiry-refresh complexity to get wrong for a call this infrequent.
    /// </summary>
    public interface IReconciliationTokenProvider
    {
        /// <summary>Mints a fresh reconciliation access token.</summary>
        Task<PrintFlowResult<string>> GetTokenAsync(CancellationToken cancellationToken);
    }

    /// <summary>Request body for <c>POST api/auth/service-token</c>, matching the Inventory API's <c>ServiceTokenRequest</c>.</summary>
    public sealed class ServiceTokenRequestDto
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    /// <summary>Response body from <c>POST api/auth/service-token</c>, matching the Inventory API's <c>ServiceTokenResponse</c>.</summary>
    public sealed class ServiceTokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    /// <inheritdoc cref="IReconciliationTokenProvider" />
    public sealed class ReconciliationTokenProvider : IReconciliationTokenProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly ReconciliationCredentialOptions _credential;
        private readonly Logger _log;

        /// <summary>
        /// Creates the provider over a typed <see cref="HttpClient"/> (registered via
        /// <c>AddHttpClient&lt;IReconciliationTokenProvider, ReconciliationTokenProvider&gt;</c> in
        /// <c>Program.cs</c>, same base address as <see cref="IPrintFlowClient"/> - the token
        /// endpoint lives on the same Inventory API).
        /// </summary>
        public ReconciliationTokenProvider(
            HttpClient httpClient, IOptions<ReconciliationCredentialOptions> credential, Logger log)
        {
            _httpClient = httpClient;
            _credential = credential.Value;
            _log = log;
        }

        /// <inheritdoc />
        public async Task<PrintFlowResult<string>> GetTokenAsync(CancellationToken cancellationToken)
        {
            var request = new ServiceTokenRequestDto
            {
                ClientId = _credential.ClientId,
                ClientSecret = _credential.ClientSecret
            };

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/service-token")
                {
                    Content = JsonContent.Create(request)
                };

                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                string body = await response.Content.ReadAsStringAsync(cancellationToken);

                // Mirrors PrintFlowClient.ParseAsync's transient/business-failure distinction
                // exactly, duplicated rather than extracted into a shared helper - this file is
                // the only other caller, and touching the already-shipped PrintFlowClient for one
                // new consumer wasn't worth it.
                if ((int)response.StatusCode >= 500 || string.IsNullOrWhiteSpace(body))
                {
                    return PrintFlowResult<string>.Transient($"HTTP {(int)response.StatusCode}");
                }

                ApiEnvelope<ServiceTokenResponseDto>? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<ApiEnvelope<ServiceTokenResponseDto>>(body, JsonOptions);
                }
                catch (JsonException)
                {
                    return PrintFlowResult<string>.Transient($"HTTP {(int)response.StatusCode} with an unparseable body");
                }

                if (envelope is null)
                {
                    return PrintFlowResult<string>.Transient($"HTTP {(int)response.StatusCode} with an empty body");
                }

                if (envelope.Success && envelope.Data is not null)
                {
                    return PrintFlowResult<string>.Ok(envelope.Data.AccessToken);
                }

                // Invalid or revoked credential - not transient. Retrying without fixing the
                // credential itself won't help, same reasoning as a business rejection on
                // print-result. Logged with the error code only, never the secret.
                _log.AppendLog(
                    $"Reconciliation token mint rejected: {envelope.Error?.Code}", Logger.LogType.Error);
                return PrintFlowResult<string>.Business(envelope.Error?.Code, envelope.Error?.Message);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.AppendLog("Reconciliation token mint failed (transient): " + ex.Message, Logger.LogType.Error);
                return PrintFlowResult<string>.Transient(ex.Message);
            }
        }
    }
}
