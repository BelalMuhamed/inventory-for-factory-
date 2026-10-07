using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Machine.Evolis;
using invetoryBackGroundServices.Options;
using invetoryBackGroundServices.Params;
using invetoryBackGroundServices.PrintFlow;
using MATICA_S3300e.LAN;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Services
{
    /// <summary>
    /// Runs Evolis discovery and pushes the result to the Inventory API's
    /// supervisor-registration endpoint (Evolis Integration Plan §8, decision Q-21) — once at
    /// startup (when <see cref="EvolisOptions.AutoRegisterSupervisorOnStartup"/>), and again on
    /// demand after any manual discovery call (<c>EvolisMachineController.GetPrinters</c>).
    /// Failure here is always logged, never fatal — the Agent must still serve print requests if
    /// the Inventory API is briefly unreachable at startup.
    /// </summary>
    public sealed class EvolisSupervisorRegistrar : IHostedService
    {
        private readonly IReconciliationTokenProvider _tokenProvider;
        private readonly IEvolisSupervisorRegistrationClient _registrationClient;
        private readonly EvolisDeviceDiscovery _discovery;
        private readonly EvolisOptions _options;
        private readonly Logger _log;

        /// <summary>Creates the registrar.</summary>
        public EvolisSupervisorRegistrar(
            IReconciliationTokenProvider tokenProvider,
            IEvolisSupervisorRegistrationClient registrationClient,
            EvolisDeviceDiscovery discovery,
            IOptions<EvolisOptions> options,
            Logger log)
        {
            _tokenProvider = tokenProvider;
            _registrationClient = registrationClient;
            _discovery = discovery;
            _options = options.Value;
            _log = log;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.AutoRegisterSupervisorOnStartup)
            {
                return Task.CompletedTask;
            }

            // Deliberately not awaited: startup must not block on the Inventory API being
            // reachable (plan §8 acceptance criteria).
            _ = RegisterNowAsync(cancellationToken);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Runs discovery and registers the result. Never throws — every failure path is logged
        /// and swallowed, since neither a startup caller nor a manual-discovery caller should
        /// ever fail because this side effect couldn't complete.
        /// </summary>
        public async Task RegisterNowAsync(CancellationToken cancellationToken)
        {
            try
            {
                List<EvolisDiscoveredPrinterDto> devices = _discovery.DiscoverAll();

                PrintFlowResult<string> tokenResult = await _tokenProvider.GetTokenAsync(cancellationToken);
                if (!tokenResult.Success || tokenResult.Data is null)
                {
                    _log.AppendLog(
                        "Evolis supervisor registration skipped: could not obtain a reconciliation token: " +
                        tokenResult.ErrorMessage, Logger.LogType.Error);
                    return;
                }

                string accessToken = tokenResult.Data;
                long? branchId = ReadBranchIdClaim(accessToken);
                if (branchId is null)
                {
                    _log.AppendLog(
                        "Evolis supervisor registration skipped: reconciliation token has no branchId claim.",
                        Logger.LogType.Error);
                    return;
                }

                List<DiscoveredDeviceDto> deviceDtos = devices.Select(d => new DiscoveredDeviceDto
                {
                    DeviceName = d.DeviceName,
                    Model = d.Model,
                    Link = d.Link,
                    DriverVersion = d.DriverVersion,
                    IsSupervised = d.IsSupervised
                }).ToList();

                PrintFlowResult<SupervisorRegistrationResultDto> result = await _registrationClient.RegisterAsync(
                    accessToken, branchId.Value, Environment.MachineName, deviceDtos, cancellationToken);

                if (!result.Success)
                {
                    _log.AppendLog("Evolis supervisor registration failed: " + result.ErrorMessage, Logger.LogType.Error);
                }
                else
                {
                    _log.AppendLog(
                        $"Evolis supervisor registration completed: {result.Data!.Matched.Count} matched, " +
                        $"{result.Data.Unmatched.Count} unmatched, {result.Data.RegisteredButNotDiscovered.Count} not discovered.",
                        Logger.LogType.Info);
                }
            }
            catch (Exception ex)
            {
                _log.AppendLog("Evolis supervisor registration threw unexpectedly: " + ex.Message, Logger.LogType.Error);
            }
        }

        private static long? ReadBranchIdClaim(string jwt)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                JwtSecurityToken token = handler.ReadJwtToken(jwt);
                string? value = token.Claims.FirstOrDefault(c => c.Type == "branchId")?.Value;
                return long.TryParse(value, out long id) ? id : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
