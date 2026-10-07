using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using invetoryBackGroundServices.Machine.Abstractions;
using invetoryBackGroundServices.Options;
using invetoryBackGroundServices.Params;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace invetoryBackGroundServices.Machine.Evolis
{
    /// <summary>
    /// <see cref="ICardPrinterAdapter"/> for the Evolis family (Evolis Integration Plan §8/§10).
    /// No <c>Evolis.*</c> type crosses out of this class's return values; it converts
    /// <see cref="EvolisDeviceDiscovery"/>'s already-vendor-specific DTOs into the vendor-neutral
    /// <see cref="PrinterSnapshot"/> the rest of the system is allowed to see, and
    /// <see cref="OpenAsync"/> wraps a freshly-opened <see cref="EvolisConnectionScope"/> in an
    /// <see cref="EvolisPrinterConnection"/> for the orchestrator to drive.
    /// </summary>
    public sealed class EvolisPrinterAdapter : ICardPrinterAdapter
    {
        private readonly EvolisDeviceDiscovery _discovery;
        private readonly EvolisConnectionFactory _connectionFactory;
        private readonly EvolisOptions _options;
        private readonly ILogger<EvolisPrinterAdapter> _logger;

        /// <summary>Creates the adapter.</summary>
        public EvolisPrinterAdapter(
            EvolisDeviceDiscovery discovery, EvolisConnectionFactory connectionFactory,
            IOptions<EvolisOptions> options, ILogger<EvolisPrinterAdapter> logger)
        {
            _discovery = discovery;
            _connectionFactory = connectionFactory;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<PrinterSnapshot>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PrinterSnapshot> snapshots = _discovery.DiscoverAll().Select(ToSnapshot).ToList();
            return Task.FromResult(snapshots);
        }

        /// <inheritdoc />
        public async Task<PrinterSnapshot?> GetSnapshotAsync(string deviceName, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PrinterSnapshot> all = await DiscoverAsync(cancellationToken);
            return all.FirstOrDefault(s => s.DeviceName == deviceName);
        }

        /// <inheritdoc />
        public Task<ICardPrinterConnection> OpenAsync(CardJob job, CancellationToken cancellationToken = default) =>
            Task.Run<ICardPrinterConnection>(() =>
            {
                EvolisConnectionScope scope = _connectionFactory.Open(job.DeviceName, reserve: true);
                var connection = new EvolisPrinterConnection(scope, _options, _logger);
                connection.ApplyConfiguration(job.InputTray, job.OutputTray, job.ErrorTray, job.MagCoercivity);
                return connection;
            }, cancellationToken);

        private static PrinterSnapshot ToSnapshot(EvolisDiscoveredPrinterDto d) => new(
            d.DeviceName,
            d.DisplayName,
            d.Model,
            d.Uri,
            d.Link,
            d.IsOnline,
            d.IsSupervised,
            d.SupervisedByEps,
            d.DriverVersion,
            // Preserved as null, not collapsed to false: a consumer that reads Magnetic/Flip/etc.
            // off a false default would see a confirmed "no" instead of "we don't know" — the
            // exact distinction EvolisDeviceDiscovery's graceful degradation exists to preserve
            // (see CLAUDE.md's Phase 3 section: GetStatus genuinely fails for some real devices).
            d.Capabilities is null
                ? null
                : new PrinterCapabilities(
                    d.Capabilities.Magnetic, d.Capabilities.Flip, d.Capabilities.Smart,
                    d.Capabilities.Contactless, d.Capabilities.Ethernet),
            d.Ribbon?.SdkCode,
            d.State);
    }
}
