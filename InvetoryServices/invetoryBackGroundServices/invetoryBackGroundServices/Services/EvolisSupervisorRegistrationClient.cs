using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.PrintFlow;
using MATICA_S3300e.LAN;

namespace invetoryBackGroundServices.Services
{
    /// <inheritdoc cref="IEvolisSupervisorRegistrationClient" />
    public sealed class EvolisSupervisorRegistrationClient : IEvolisSupervisorRegistrationClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly Logger _log;

        /// <summary>
        /// Creates the client over a typed <see cref="HttpClient"/> (registered via
        /// <c>AddHttpClient&lt;IEvolisSupervisorRegistrationClient, EvolisSupervisorRegistrationClient&gt;</c>
        /// in <c>Program.cs</c>, same base address as <see cref="IReconciliationTokenProvider"/> — the
        /// registration endpoint lives on the same Inventory API).
        /// </summary>
        public EvolisSupervisorRegistrationClient(HttpClient httpClient, Logger log)
        {
            _httpClient = httpClient;
            _log = log;
        }

        /// <inheritdoc />
        public async Task<PrintFlowResult<SupervisorRegistrationResultDto>> RegisterAsync(
            string accessToken,
            long branchId,
            string hostName,
            IReadOnlyList<DiscoveredDeviceDto> devices,
            CancellationToken cancellationToken)
        {
            var request = new SupervisorRegistrationRequestDto
            {
                BranchId = branchId,
                HostName = hostName,
                Devices = devices
            };

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/printers/supervisor-registration")
                {
                    Content = JsonContent.Create(request)
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                string body = await response.Content.ReadAsStringAsync(cancellationToken);

                // Mirrors ReconciliationTokenProvider.GetTokenAsync's transient/business-failure
                // distinction exactly — same reasoning, this is the other caller of that pattern.
                if ((int)response.StatusCode >= 500 || string.IsNullOrWhiteSpace(body))
                {
                    return PrintFlowResult<SupervisorRegistrationResultDto>.Transient($"HTTP {(int)response.StatusCode}");
                }

                ApiEnvelope<SupervisorRegistrationResultDto>? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<ApiEnvelope<SupervisorRegistrationResultDto>>(body, JsonOptions);
                }
                catch (JsonException)
                {
                    return PrintFlowResult<SupervisorRegistrationResultDto>.Transient(
                        $"HTTP {(int)response.StatusCode} with an unparseable body");
                }

                if (envelope is null)
                {
                    return PrintFlowResult<SupervisorRegistrationResultDto>.Transient(
                        $"HTTP {(int)response.StatusCode} with an empty body");
                }

                if (envelope.Success && envelope.Data is not null)
                {
                    return PrintFlowResult<SupervisorRegistrationResultDto>.Ok(envelope.Data);
                }

                _log.AppendLog(
                    $"Supervisor registration rejected: {envelope.Error?.Code}", Logger.LogType.Error);
                return PrintFlowResult<SupervisorRegistrationResultDto>.Business(envelope.Error?.Code, envelope.Error?.Message);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.AppendLog("Supervisor registration failed (transient): " + ex.Message, Logger.LogType.Error);
                return PrintFlowResult<SupervisorRegistrationResultDto>.Transient(ex.Message);
            }
        }
    }
}
