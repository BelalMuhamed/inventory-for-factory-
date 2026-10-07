using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace invetoryBackGroundServices.Controllers
{
    /// <summary>
    /// TEMPORARY proof-of-concept only - not wired into the real Print flow, doesn't touch
    /// MachineCommands.cs, doesn't replace anything. Demonstrates converting exactly one Hardware
    /// I/O operation (GetInfoJson) from the existing HttpWebRequest/ref-parameter pattern to a
    /// real async HttpClient pattern, for side-by-side comparison against the existing sync
    /// endpoint (POST api/Machine/get-machine-info) on real hardware.
    ///
    /// No [Authorize] on purpose - this is for your own manual testing only. Delete this
    /// controller once the approach is validated; it must never ship as-is.
    /// </summary>
    [Route("api/poc")]
    [ApiController]
    public sealed class HardwareIoPocController : ControllerBase
    {
        /// <summary>
        /// Async POC endpoint. Call this against the same machine IP you'd normally pass to
        /// POST api/Machine/get-machine-info, and compare the results.
        /// </summary>
        [HttpPost("get-info-json-async-poc")]
        public async Task<IActionResult> GetInfoJsonAsyncPoc(
            [FromQuery] string ip, [FromQuery] string action = "action", CancellationToken cancellationToken = default)
        {
            // Mirrors CommandManagement's own JsonCommand construction for the GetInfoJson case
            // exactly: JsonCommand = "{\"Command\":\"" + Command.ToString() + "\"}";
            const string jsonCommand = "{\"Command\":\"GetInfoJson\"}";

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                string responseText = await GetInfoJsonAsync(ip, action, jsonCommand, cancellationToken);
                stopwatch.Stop();

                return Ok(new
                {
                    success = true,
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                    rawResponse = responseText
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // Mirrors CommandManagement's outer catch (Exception ex) { sMessage = ex.Message;
                // return -1; } as closely as an HTTP response can, so a failure here reads the
                // same way a failure from the sync path would.
                return StatusCode(502, new
                {
                    success = false,
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                    exceptionType = ex.GetType().Name,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Async, HttpClient-based equivalent of MachineCommands.httpPOSTGetInfoJson.
        /// Same URL shape ("https://{ip}:33201/{action}"), same 10-second overall timeout, same
        /// certificate-bypass behavior (still required for this self-signed LAN device - changing
        /// that here would mean this test compares a different code path than what's actually
        /// running today, which defeats the point).
        ///
        /// The original method builds its request body via an odd UTF16->UTF8-repacked-as-chars
        /// round-trip before handing it to StreamWriter. For GetInfoJson's request body specifically
        /// ('{"Command":"GetInfoJson"}', pure ASCII), that round-trip is provably a no-op: ASCII
        /// bytes survive UTF-8 encode/decode unchanged either way, so StringContent(jsonCommand,
        /// Encoding.UTF8, ...) below produces byte-identical output on the wire. Not replicated
        /// literally because it doesn't change anything for this command - if you extend this
        /// pattern to a command with non-ASCII embossing text later, re-check this assumption
        /// rather than carrying it forward blindly.
        ///
        /// No 'ref' parameter, per your requirement to keep those out of the async version -
        /// 'ref'/'out' can't appear on an async method's signature at all, so this returns the
        /// response body directly instead.
        ///
        /// Deliberately NOT using IHttpClientFactory here, even though it's already registered in
        /// this project (PrintFlowClient uses it) - wiring a new named client requires a
        /// Program.cs change, which you asked to avoid for this test. A new HttpClient per call is
        /// an anti-pattern in general (documented in the earlier review), but for a POC you'll call
        /// a handful of times by hand, it's a non-issue. The real refactor should use a properly
        /// pooled, named client the same way PrintFlowClient already does.
        /// </summary>
        private static async Task<string> GetInfoJsonAsync(
            string ip, string action, string jsonCommand, CancellationToken cancellationToken)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(100) // matches httpWebRequest.Timeout = 10000 exactly
            };

            string url = $"https://{ip}/{action}";
            using var content = new StringContent(jsonCommand, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await client.PostAsync(url, content, cancellationToken);

            // HttpWebRequest.GetResponse() throws automatically on a non-2xx status - that's what
            // CommandManagement's catch block is actually catching today. PostAsync doesn't throw
            // on its own, so this line is what makes the two behave the same way on a bad status.
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }
}