using System.Collections.Generic;
using Evolis;
using invetoryBackGroundServices.Machine.Abstractions;
using invetoryBackGroundServices.Params;
using Microsoft.Extensions.Logging;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// Device-proxy discovery (Evolis Integration Plan §8, decision Q-22): calls
    /// <c>Evolis.GetDevices()</c>, then briefly opens each device read-only (no
    /// <c>Reserve()</c> — that would lock the LCD and disrupt a walk-up operator) to read its
    /// status and loaded ribbon before closing again.
    /// <para>
    /// <b>Live hardware finding (see <c>CLAUDE.md</c>'s Phase 3 section):</b> on at least this
    /// machine's Evolis Primacy 2, a live status read (<c>GetStatus</c>, <c>SendStatusRequest</c>,
    /// even after <c>EnableStatus</c>) fails outright regardless of whether Evolis's own driver
    /// services are running — confirmed by stopping them and re-testing. <c>GetInfo</c> and
    /// <c>GetRibbonInfo</c> are unaffected and always succeeded in testing. Per plan rule §13.4
    /// #7, a failed status read degrades this device's <c>Capabilities</c>/<c>SupervisedByEps</c>
    /// to null and <c>State</c> to <see cref="PrinterState.Unknown"/> — it never fails the whole
    /// discovery call, and every other field (name, model, ribbon) is still populated.
    /// </para>
    /// </summary>
    public sealed class EvolisDeviceDiscovery
    {
        private readonly EvolisConnectionFactory _connectionFactory;
        private readonly ILogger<EvolisDeviceDiscovery> _logger;

        /// <summary>Creates the discovery service.</summary>
        public EvolisDeviceDiscovery(EvolisConnectionFactory connectionFactory, ILogger<EvolisDeviceDiscovery> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        /// <summary>Lists every Evolis device this machine can see, each briefly probed for status and ribbon.</summary>
        public List<EvolisDiscoveredPrinterDto> DiscoverAll()
        {
            List<Device> devices = global::Evolis.Evolis.GetDevices();
            var result = new List<EvolisDiscoveredPrinterDto>(devices.Count);

            foreach (Device device in devices)
            {
                result.Add(ProbeDevice(device));
            }

            return result;
        }

        private EvolisDiscoveredPrinterDto ProbeDevice(Device device)
        {
            EvolisCapabilitiesDto? capabilities = null;
            bool? supervisedByEps = null;
            EvolisRibbonDto? ribbon = null;
            PrinterState state = PrinterState.Unknown;

            try
            {
                using EvolisConnectionScope scope = _connectionFactory.Open(device.name, reserve: false);
                Connection connection = scope.Connection;

                // IsOpen() is deprecated in favor of GetState(), but it is still functional and
                // the simplest true/false answer to "did the connection actually open" — a
                // deliberate choice, not an oversight.
#pragma warning disable CS0618
                if (!connection.IsOpen())
#pragma warning restore CS0618
                {
                    _logger.LogWarning(
                        "Could not open a connection to Evolis device '{DeviceName}': {LastError}",
                        device.name, connection.GetLastError());
                }
                else
                {
                    Status status = default;
                    bool statusOk = connection.GetStatus(out status) || connection.SendStatusRequest(out status);
                    if (statusOk)
                    {
                        capabilities = new EvolisCapabilitiesDto(
                            status.IsOn(Status.Flag.CFG_MAGNETIC),
                            status.IsOn(Status.Flag.CFG_FLIP),
                            status.IsOn(Status.Flag.CFG_SMART),
                            status.IsOn(Status.Flag.CFG_CONTACTLESS),
                            status.IsOn(Status.Flag.CFG_ETHERNET));
                        supervisedByEps = status.IsOn(Status.Flag.INF_PCSVC_LOCKED);
                        state = EvolisStatusMapper.MapState(status);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "GetStatus failed for Evolis device '{DeviceName}': {LastError}. " +
                            "capabilities/supervisedByEps/state will be reported as unknown.",
                            device.name, connection.GetLastError());
                    }

                    RibbonInfo ribbonInfo = default;
                    if (connection.GetRibbonInfo(out ribbonInfo))
                    {
                        ribbon = new EvolisRibbonDto(ribbonInfo.type.ToString());
                    }
                    else
                    {
                        _logger.LogWarning(
                            "GetRibbonInfo failed for Evolis device '{DeviceName}': {LastError}",
                            device.name, connection.GetLastError());
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Never let a single device's probe failure take down the whole discovery call -
                // the caller still gets every other device, and this one with degraded fields.
                _logger.LogError(ex, "Unexpected failure probing Evolis device '{DeviceName}'", device.name);
            }

            return new EvolisDiscoveredPrinterDto(
                device.name,
                device.displayName,
                global::Evolis.Evolis.GetModelName(device.model),
                device.uri,
                device.link.ToString(),
                device.isOnline,
                device.isSupervised,
                device.driverVersion,
                capabilities,
                supervisedByEps,
                ribbon,
                state);
        }
    }
}
