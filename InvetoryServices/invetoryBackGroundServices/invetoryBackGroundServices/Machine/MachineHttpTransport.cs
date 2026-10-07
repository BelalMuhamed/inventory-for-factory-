using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Options;
using MATICA_S3300e.LAN;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Machine
{
    /// <summary>
    /// Generic, vendor-neutral async transport for card-machine communication: given a host, port,
    /// action path and a JSON body, POST it and return the raw response text. Contains no Matica
    /// specifics whatsoever - no command names, no response shapes - which is the seam that makes
    /// adding another device/vendor later a matter of writing a new command layer rather than
    /// rewriting communication code.
    /// </summary>
    public interface IMachineTransport
    {
        /// <summary>
        /// Sends <paramref name="jsonBody"/> to <c>https://{host}:{port}/{action}</c> and returns
        /// the response body as text.
        /// </summary>
        /// <exception cref="MachineTimeoutException">The machine did not respond in time.</exception>
        /// <exception cref="MachineCommunicationException">The machine could not be reached.</exception>
        Task<string> PostAsync(
            string host, string port, string action, string jsonBody, CancellationToken cancellationToken);
    }

    /// <summary>
    /// <see cref="HttpClient"/>-based <see cref="IMachineTransport"/>, replacing the blocking
    /// <see cref="System.Net.HttpWebRequest"/> pattern used by the pre-existing synchronous
    /// methods (which remain untouched until every call site has migrated). This is the pattern
    /// validated by the POC endpoint against the real machine.
    /// </summary>
    public sealed class MachineHttpTransport : IMachineTransport
    {
        private readonly HttpClient _httpClient;
        private readonly MachineCommunicationOptions _options;
        private readonly Logger _log;

        /// <summary>Creates the transport from its pooled client, options and logger.</summary>
        public MachineHttpTransport(
            HttpClient httpClient, IOptions<MachineCommunicationOptions> options, Logger log)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _log = log;
        }

        /// <inheritdoc />
        public async Task<string> PostAsync(
            string host, string port, string action, string jsonBody, CancellationToken cancellationToken)
        {
            string url = $"https://{host}:{port}/{action}";
            _log.AppendLog($"Command >> {url} :: {jsonBody}", Logger.LogType.Info);

            // Separate from the HttpClient's own Timeout so a caller-supplied cancellation and a
            // configured timeout are distinguishable below.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            try
            {
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using HttpResponseMessage response =
                    await _httpClient.PostAsync(url, content, timeoutCts.Token);

                string body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                _log.AppendLog($"Response << {body}", Logger.LogType.Info);

                if (!response.IsSuccessStatusCode)
                {
                    // HttpWebRequest.GetResponse() threw on non-2xx, which is what the old
                    // CommandManagement catch block was actually catching. Preserved here as an
                    // explicit typed failure rather than a silent -1.
                    throw new MachineCommunicationException(
                        $"The machine returned HTTP {(int)response.StatusCode}.");
                }

                return body;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out on our own budget rather than the caller cancelling the request.
                _log.AppendLog($"Machine timeout after {_options.TimeoutSeconds}s: {url}", Logger.LogType.Error);
                throw new MachineTimeoutException(
                    $"No response within {_options.TimeoutSeconds} seconds.");
            }
            catch (HttpRequestException ex)
            {
                _log.AppendLog($"Machine communication failure: {url} :: {ex.Message}", Logger.LogType.Error);
                throw new MachineCommunicationException(ex.Message, ex);
            }
        }
    }
}
