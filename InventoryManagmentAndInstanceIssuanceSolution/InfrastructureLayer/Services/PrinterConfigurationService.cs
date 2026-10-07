using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Common;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Printing;
using ApplicationLayer.Errors;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Printer registry management service (ERD §6, Printing Module decisions Q-01/Q-09).
    /// Resolves the caller from <see cref="ICurrentTenant"/>, matching
    /// <see cref="ProductService"/>'s pattern; unlike <c>ProductService</c>, every write here is
    /// additionally gated to a system admin (decision Q-09) — a tenant caller gets
    /// <see cref="PrintingErrors.PrinterOnlySystemAdmin"/> rather than the write ever reaching a
    /// scope check. <see cref="RegisterSupervisorAsync"/> is the one exception: it is reached only
    /// via the Agent's reconciliation service-account token, never a tenant/admin session, so it
    /// has no system-admin check of its own (Evolis Integration Plan §7).
    /// </summary>
    public sealed class PrinterConfigurationService : IPrinterConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentTenant _currentTenant;
        private readonly ILogger<PrinterConfigurationService> _logger;

        public PrinterConfigurationService(
            IUnitOfWork unitOfWork, ICurrentTenant currentTenant, ILogger<PrinterConfigurationService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentTenant = currentTenant;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<Result<PaginatedResponse<PrinterResponse>>> GetAllAsync(
            PrinterListFilter filter, CancellationToken cancellationToken = default)
        {
            long? scope = ResolveScope(out Error? error);
            if (error is not null) return Result.Failure<PaginatedResponse<PrinterResponse>>(error);

            (IReadOnlyList<Printer> items, int total) =
                await _unitOfWork.Printers.GetPagedAsync(scope, filter, cancellationToken);
            IReadOnlyDictionary<long, MaticaPrinterConfiguration> maticaConfigs =
                await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdsAsync(
                    items.Select(p => p.Id), cancellationToken);

            IReadOnlyDictionary<long, EvolisPrinterConfiguration> evolisConfigs =
                await _unitOfWork.EvolisPrinterConfigs.GetByPrinterIdsAsync(
                    items.Select(p => p.Id), cancellationToken);

            IReadOnlyList<PrinterResponse> data =
                items.Select(p => Map(
                    p,
                    maticaConfigs.GetValueOrDefault(p.Id),
                    evolisConfigs.GetValueOrDefault(p.Id))).ToList();

            return PaginatedResponse<PrinterResponse>.Create(data, filter.Page, filter.PageSize, total);
        }

        /// <inheritdoc />
        public async Task<Result<PrinterResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<PrinterResponse>(error);

            MaticaPrinterConfiguration? maticaConfig = printer!.UsingPrinterType == UsingPrinterType.Matica
     ? await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdAsync(printer.Id, cancellationToken)
     : null;

            EvolisPrinterConfiguration? evolisConfig = printer.UsingPrinterType == UsingPrinterType.Evolis
                ? await _unitOfWork.EvolisPrinterConfigs.GetByPrinterIdAsync(printer.Id, cancellationToken)
                : null;

            return Map(printer, maticaConfig, evolisConfig);
        }

        /// <inheritdoc />
        public async Task<Result<PrinterResponse>> CreateAsync(
            CreatePrinterRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterOnlySystemAdmin());
            }

            if (request.TenantId is not long targetTenantId)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterTenantRequired());
            }

            if (await _unitOfWork.Tenants.GetByIdIncludingDeletedAsync(targetTenantId, cancellationToken) is null)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterTargetTenantNotFound(targetTenantId));
            }

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != targetTenantId)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchNotFound(request.BranchId));
            }

            if (branch.IsDeleted)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchDeleted(request.BranchId));
            }

            Result shapeCheck = ValidatePrinterConfigPayloadShape(
                request.UsingPrinterType, request.MaticaConfig, request.EvolisConfig);
            if (shapeCheck.IsFailure)
            {
                return Result.Failure<PrinterResponse>(shapeCheck.Error);
            }

            if (await _unitOfWork.Printers.UniqueNumberExistsAsync(
                    targetTenantId, request.UniqueNumber, null, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterDuplicateUniqueNumber(request.UniqueNumber));
            }

            // printer, maticaConfig and evolisConfig are declared here — outside the transaction
            // lambda — for the same reason as DisposalService/TransferService.SettleAsync:
            // printer.Id is 0 until SaveChanges runs, which happens after the lambda returns, so
            // the response below must be built from these same instances after the transaction
            // completes, not from inside it.
            var printer = new Printer
            {
                TenantId = targetTenantId,
                BranchId = request.BranchId,
                Branch = branch,
                UsingPrinterType = request.UsingPrinterType,
                Name = request.Name,
                Model = request.Model,
                UniqueNumber = request.UniqueNumber,
            };
            MaticaPrinterConfiguration? maticaConfig = null;
            EvolisPrinterConfiguration? evolisConfig = null;

            Result transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _unitOfWork.Printers.AddAsync(printer, cancellationToken);

                // Setting the Printer navigation (not PrinterId directly) on each config below
                // lets EF Core's change tracker fix up the foreign key from the printer's
                // generated id once it exists, rather than requiring a second round trip.
                if (request.UsingPrinterType == UsingPrinterType.Matica)
                {
                    maticaConfig = new MaticaPrinterConfiguration
                    {
                        Printer = printer,
                        FeederId = request.MaticaConfig!.FeederId,
                        HopperId = request.MaticaConfig.HopperId,
                        RejectedId = request.MaticaConfig.RejectedId,
                        Port = request.MaticaConfig.Port,
                        IpAddress = request.MaticaConfig.IpAddress,
                        TipperTemperature = request.MaticaConfig.TipperTemperature,
                        TipperPressure = request.MaticaConfig.TipperPressure,
                        TipperConsumption = request.MaticaConfig.TipperConsumption,
                        TipperTime = request.MaticaConfig.TipperTime,
                    };
                    await _unitOfWork.MaticaPrinterConfigs.AddAsync(maticaConfig, cancellationToken);
                }
                else if (request.UsingPrinterType == UsingPrinterType.Evolis)
                {
                    evolisConfig = new EvolisPrinterConfiguration
                    {
                        Printer = printer,
                        TenantId = targetTenantId,
                        DeviceName = request.EvolisConfig!.DeviceName.Trim(),
                        DefaultRibbonTypeId = request.EvolisConfig.DefaultRibbonTypeId,
                        MagCoercivity = request.EvolisConfig.MagCoercivity,
                        InputTray = request.EvolisConfig.InputTray,
                        OutputTray = request.EvolisConfig.OutputTray,
                        ErrorTray = request.EvolisConfig.ErrorTray,
                        OpenMode = request.EvolisConfig.OpenMode,
                        ErrorManagement = request.EvolisConfig.ErrorManagement,
                        // SupervisorHostName / SupervisorRegisteredAt: deliberately left null —
                        // not accepted from this payload; written only by Phase 2's
                        // self-registration route.
                    };
                    await _unitOfWork.EvolisPrinterConfigs.AddAsync(evolisConfig, cancellationToken);
                }
                if (request.UsingPrinterType == UsingPrinterType.Evolis)
                {
                    Result deviceNameCheck = ValidateDeviceName(request.EvolisConfig!.DeviceName);
                    if (deviceNameCheck.IsFailure)
                    {
                        return deviceNameCheck;
                    }

                    if (request.EvolisConfig.DefaultRibbonTypeId is long ribbonTypeId &&
                        !await _unitOfWork.RibbonTypes.ExistsAsync(ribbonTypeId, cancellationToken))
                    {
                        return Result.Failure(PrintingErrors.DefaultRibbonTypeNotFound(ribbonTypeId));
                    }
                }
                return Result.Success();
            }, cancellationToken);

            if (transactionResult.IsFailure)
            {
                return Result.Failure<PrinterResponse>(transactionResult.Error);
            }

            return Result.Success(Map(printer, maticaConfig, evolisConfig));
        }

        /// <inheritdoc />
        public async Task<Result<PrinterResponse>> UpdateAsync(
            long id, UpdatePrinterRequest request, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterOnlySystemAdmin());
            }

            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure<PrinterResponse>(error);

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(request.BranchId, cancellationToken);
            if (branch is null || branch.TenantId != printer!.TenantId)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchNotFound(request.BranchId));
            }

            if (branch.IsDeleted)
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterBranchDeleted(request.BranchId));
            }

            // UsingPrinterType is immutable (confirmed decision, Printing Module P2 review) — the
            // shape check validates the request's MaticaConfig against the printer's existing,
            // unchangeable family, not against anything in UpdatePrinterRequest.
            Result shapeCheck = ValidatePrinterConfigPayloadShape(
                printer.UsingPrinterType, request.MaticaConfig, request.EvolisConfig);
            if (shapeCheck.IsFailure)
            {
                return Result.Failure<PrinterResponse>(shapeCheck.Error);
            }

            if (!string.Equals(printer.UniqueNumber, request.UniqueNumber, StringComparison.Ordinal) &&
                await _unitOfWork.Printers.UniqueNumberExistsAsync(printer.TenantId, request.UniqueNumber, id, cancellationToken))
            {
                return Result.Failure<PrinterResponse>(PrintingErrors.PrinterDuplicateUniqueNumber(request.UniqueNumber));
            }

            // Declared here, outside the transaction lambda, for the same reason as in
            // CreateAsync above: printer.UpdatedAt and any newly-self-healed config row's fields
            // are only current after SaveChanges runs, so the response is built from these same
            // instances after the transaction completes.
            MaticaPrinterConfiguration? maticaConfig = null;
            EvolisPrinterConfiguration? evolisConfig = null;

            Result transactionResult = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                printer.BranchId = request.BranchId;
                printer.Branch = branch;
                printer.Name = request.Name;
                printer.Model = request.Model;
                printer.UniqueNumber = request.UniqueNumber;
                _unitOfWork.Printers.Update(printer);

                if (printer.UsingPrinterType == UsingPrinterType.Matica)
                {
                    // Includes a soft-deleted row on purpose: PrinterId is uniquely constrained
                    // unconditionally, so if the filtered lookup missed an existing-but-deleted
                    // row, inserting a "new" one here would fail that constraint outright.
                    maticaConfig = await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdIncludingDeletedAsync(
                        printer.Id, cancellationToken);

                    if (maticaConfig is null)
                    {
                        // Defensive self-heal: creation always pairs the two rows, so this
                        // shouldn't normally happen, but an update should not fail outright just
                        // because the extension row is somehow missing entirely.
                        maticaConfig = new MaticaPrinterConfiguration { Printer = printer };
                        await _unitOfWork.MaticaPrinterConfigs.AddAsync(maticaConfig, cancellationToken);
                    }
                    else if (maticaConfig.IsDeleted)
                    {
                        // Self-heal the other inconsistent state: the printer is alive but its
                        // extension row was left soft-deleted (e.g. a prior restore that predates
                        // this fix). An explicit update is treated as reaffirming the printer is
                        // Matica, so un-delete it rather than leave the mismatch in place.
                        maticaConfig.IsDeleted = false;
                        maticaConfig.DeletedAt = null;
                        maticaConfig.DeletedBy = null;
                    }

                    maticaConfig.FeederId = request.MaticaConfig!.FeederId;
                    maticaConfig.HopperId = request.MaticaConfig.HopperId;
                    maticaConfig.RejectedId = request.MaticaConfig.RejectedId;
                    maticaConfig.Port = request.MaticaConfig.Port;
                    maticaConfig.IpAddress = request.MaticaConfig.IpAddress;
                    maticaConfig.TipperTemperature = request.MaticaConfig.TipperTemperature;
                    maticaConfig.TipperPressure = request.MaticaConfig.TipperPressure;
                    maticaConfig.TipperConsumption = request.MaticaConfig.TipperConsumption;
                    maticaConfig.TipperTime = request.MaticaConfig.TipperTime;
                    _unitOfWork.MaticaPrinterConfigs.Update(maticaConfig);
                }
               else if (printer.UsingPrinterType == UsingPrinterType.Evolis)
                {
                    evolisConfig = await _unitOfWork.EvolisPrinterConfigs.GetByPrinterIdIncludingDeletedAsync(
                        printer.Id, cancellationToken);

                    if (evolisConfig is null)
                    {
                        evolisConfig = new EvolisPrinterConfiguration { Printer = printer, TenantId = printer.TenantId };
                        await _unitOfWork.EvolisPrinterConfigs.AddAsync(evolisConfig, cancellationToken);
                    }
                    else if (evolisConfig.IsDeleted)
                    {
                        evolisConfig.IsDeleted = false;
                        evolisConfig.DeletedAt = null;
                        evolisConfig.DeletedBy = null;
                    }

                    evolisConfig.DeviceName = request.EvolisConfig!.DeviceName.Trim();
                    evolisConfig.DefaultRibbonTypeId = request.EvolisConfig.DefaultRibbonTypeId;
                    evolisConfig.MagCoercivity = request.EvolisConfig.MagCoercivity;
                    evolisConfig.InputTray = request.EvolisConfig.InputTray;
                    evolisConfig.OutputTray = request.EvolisConfig.OutputTray;
                    evolisConfig.ErrorTray = request.EvolisConfig.ErrorTray;
                    evolisConfig.OpenMode = request.EvolisConfig.OpenMode;
                    evolisConfig.ErrorManagement = request.EvolisConfig.ErrorManagement;
                    // SupervisorHostName / SupervisorRegisteredAt intentionally untouched —
                    // owned exclusively by Phase 2's self-registration route.
                    _unitOfWork.EvolisPrinterConfigs.Update(evolisConfig);
                }
                return Result.Success();
            }, cancellationToken);

            if (transactionResult.IsFailure)
            {
                return Result.Failure<PrinterResponse>(transactionResult.Error);
            }

            return Result.Success(Map(printer, maticaConfig, evolisConfig));
        }

        /// <inheritdoc />
        public async Task<Result> SoftDeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure(PrintingErrors.PrinterOnlySystemAdmin());
            }

            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (printer!.IsDeleted) return Result.Failure(PrintingErrors.PrinterAlreadyDeleted(id));

            (long? actorId, Error? actorError) = await ResolveActorIdAsync(cancellationToken);
            if (actorError is not null) return Result.Failure(actorError);

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                printer.IsDeleted = true;
                printer.DeletedAt = DateTime.UtcNow;
                printer.DeletedBy = actorId;
                _unitOfWork.Printers.Update(printer);

                // Soft-delete cascades to the Matica extension row (same aggregate, module
                // requirement §4's reasoning applied here too) so a query respecting the standard
                // IsDeleted filter never finds a "live" configuration for a deleted printer.
                if (printer.UsingPrinterType == UsingPrinterType.Matica)
                {
                    MaticaPrinterConfiguration? maticaConfig =
                        await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdAsync(printer.Id, cancellationToken);
                    if (maticaConfig is not null)
                    {
                        maticaConfig.IsDeleted = true;
                        maticaConfig.DeletedAt = DateTime.UtcNow;
                        maticaConfig.DeletedBy = actorId;
                        _unitOfWork.MaticaPrinterConfigs.Update(maticaConfig);
                    }
                }

                return Result.Success();
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result> RestoreAsync(long id, CancellationToken cancellationToken = default)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return Result.Failure(PrintingErrors.PrinterOnlySystemAdmin());
            }

            (Printer? printer, Error? error) = await LoadScopedAsync(id, cancellationToken);
            if (error is not null) return Result.Failure(error);
            if (!printer!.IsDeleted) return Result.Failure(PrintingErrors.PrinterNotDeleted(id));

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                printer.IsDeleted = false;
                printer.DeletedAt = null;
                printer.DeletedBy = null;
                _unitOfWork.Printers.Update(printer);

                if (printer.UsingPrinterType == UsingPrinterType.Matica)
                {
                    // Must look past the standard IsDeleted filter here — the config was
                    // soft-deleted alongside the printer, so the filtered GetByPrinterIdAsync
                    // would never find it and this restore would silently leave it deleted.
                    MaticaPrinterConfiguration? maticaConfig =
                        await _unitOfWork.MaticaPrinterConfigs.GetByPrinterIdIncludingDeletedAsync(printer.Id, cancellationToken);
                    if (maticaConfig is not null && maticaConfig.IsDeleted)
                    {
                        maticaConfig.IsDeleted = false;
                        maticaConfig.DeletedAt = null;
                        maticaConfig.DeletedBy = null;
                        _unitOfWork.MaticaPrinterConfigs.Update(maticaConfig);
                    }
                }

                return Result.Success();
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Result<SupervisorRegistrationResponse>> RegisterSupervisorAsync(
            SupervisorRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.HostName))
            {
                return Result.Failure<SupervisorRegistrationResponse>(PrintingErrors.SupervisorHostNameRequired());
            }

            Branch? branch = await _unitOfWork.Branches.GetByIdIncludingDeletedAsync(request.BranchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<SupervisorRegistrationResponse>(PrintingErrors.PrinterBranchNotFound(request.BranchId));
            }

            // The full registered set, not just a lookup on the devices reported below: the third
            // partition (registeredButNotDiscovered) needs every Evolis printer this branch has,
            // regardless of whether the Agent's report mentions it.
            IReadOnlyList<EvolisPrinterConfiguration> registered =
                await _unitOfWork.EvolisPrinterConfigs.GetByTenantAndBranchAsync(
                    branch.TenantId, request.BranchId, cancellationToken);

            // Grouped by name, not a straight dictionary: DeviceName is NOT unique per branch —
            // the schema's own unique index is on (TenantId, SupervisorHostName, DeviceName), so
            // the same device name can legitimately belong to more than one registered printer
            // (e.g. two counters whose printers both report the driver's default display name).
            Dictionary<string, List<EvolisPrinterConfiguration>> registeredByName = registered
                .GroupBy(c => c.DeviceName)
                .ToDictionary(g => g.Key, g => g.ToList());

            string hostName = request.HostName.Trim();
            var matched = new List<MatchedSupervisorDevice>();
            var unmatched = new List<UnmatchedDiscoveredDevice>();
            var matchedPrinterIds = new HashSet<long>();

            foreach (DiscoveredDeviceDto device in request.Devices)
            {
                if (!registeredByName.TryGetValue(device.DeviceName, out List<EvolisPrinterConfiguration>? candidates))
                {
                    // Module requirement: a device appearing on the network must never silently
                    // become a registered asset. Reported, never auto-created.
                    unmatched.Add(new UnmatchedDiscoveredDevice(device.DeviceName, "NotRegistered"));
                    continue;
                }

                // Prefer a candidate this exact host already supervises (idempotent
                // re-affirmation); otherwise claim one that no host has claimed yet (first
                // registration). A candidate already claimed by a DIFFERENT host is never
                // silently reassigned just because another row happens to share its name.
                EvolisPrinterConfiguration? config =
                    candidates.FirstOrDefault(c => c.SupervisorHostName == hostName) ??
                    candidates.FirstOrDefault(c => c.SupervisorHostName is null);

                if (config is null)
                {
                    unmatched.Add(new UnmatchedDiscoveredDevice(device.DeviceName, "ClaimedByDifferentHost"));
                    continue;
                }

                matchedPrinterIds.Add(config.PrinterId);

                bool hostChanged = !string.Equals(config.SupervisorHostName, hostName, StringComparison.Ordinal);
                if (hostChanged && config.SupervisorHostName is not null)
                {
                    // The last machine to see the device is the one supervising it — this
                    // overwrites deliberately, but a change is worth a warning: it usually means a
                    // printer was physically moved or re-cabled to a different supervising machine.
                    _logger.LogWarning(
                        "Evolis printer {PrinterId} ({DeviceName}) supervisor host changed from '{OldHost}' to '{NewHost}'.",
                        config.PrinterId, device.DeviceName, config.SupervisorHostName, hostName);
                }

                config.SupervisorHostName = hostName;
                config.SupervisorRegisteredAt = DateTime.UtcNow;
                _unitOfWork.EvolisPrinterConfigs.Update(config);

                matched.Add(new MatchedSupervisorDevice(config.PrinterId, device.DeviceName, hostChanged));
            }

            List<UnseenRegisteredPrinter> registeredButNotDiscovered = registered
                .Where(c => !matchedPrinterIds.Contains(c.PrinterId))
                .Select(c => new UnseenRegisteredPrinter(c.PrinterId, c.DeviceName))
                .ToList();

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new SupervisorRegistrationResponse(matched, unmatched, registeredButNotDiscovered));
        }

        // Loads a printer and enforces caller scope: a tenant caller may only touch its own
        // tenant's printers, and a missing/out-of-scope printer both return NotFound (no
        // existence leak) — matches ProductService.LoadScopedAsync exactly.
        private async Task<(Printer? Printer, Error? Error)> LoadScopedAsync(long id, CancellationToken cancellationToken)
        {
            long? scope = ResolveScope(out Error? scopeError);
            if (scopeError is not null) return (null, scopeError);

            Printer? printer = await _unitOfWork.Printers.GetByIdIncludingDeletedAsync(id, cancellationToken);
            if (printer is null) return (null, PrintingErrors.PrinterNotFound(id));
            if (scope is long s && printer.TenantId != s) return (null, PrintingErrors.PrinterNotFound(id));
            return (printer, null);
        }

        // null scope => system admin (no restriction); otherwise the tenant caller's id.
        private long? ResolveScope(out Error? error)
        {
            error = null;
            if (_currentTenant.IsSystemAdmin) return null;
            if (_currentTenant.TenantId is long tenantId) return tenantId;
            error = PrintingErrors.PrinterActorNotResolved();
            return null;
        }

        private async Task<(long? ActorId, Error? Error)> ResolveActorIdAsync(CancellationToken cancellationToken)
        {
            if (!_currentTenant.IsSystemAdmin)
            {
                return (_currentTenant.TenantId,
                    _currentTenant.TenantId is null ? PrintingErrors.PrinterActorNotResolved() : null);
            }

            SystemAdmin? admin = await _unitOfWork.SystemAdmins.GetActiveByUsernameAsync(
                _currentTenant.Username!, cancellationToken);
            return admin is null ? (null, PrintingErrors.PrinterActorNotResolved()) : (admin.Id, null);
        }

        private static PrinterResponse Map(
      Printer p, MaticaPrinterConfiguration? maticaConfig, EvolisPrinterConfiguration? evolisConfig) => new(
      p.Id, p.TenantId, p.BranchId, p.Branch?.Name ?? string.Empty, p.UsingPrinterType,
      p.Name, p.Model, p.UniqueNumber,
      maticaConfig is null
          ? null
          : new MaticaPrinterConfigResponse(
              maticaConfig.FeederId, maticaConfig.HopperId, maticaConfig.RejectedId, maticaConfig.Port,
              maticaConfig.IpAddress, maticaConfig.TipperTemperature, maticaConfig.TipperPressure,
              maticaConfig.TipperConsumption, maticaConfig.TipperTime),
      evolisConfig is null
          ? null
          : new EvolisPrinterConfigResponse(
              evolisConfig.DeviceName, evolisConfig.SupervisorHostName, evolisConfig.SupervisorRegisteredAt,
              evolisConfig.DefaultRibbonTypeId, evolisConfig.MagCoercivity, evolisConfig.InputTray,
              evolisConfig.OutputTray, evolisConfig.ErrorTray, evolisConfig.OpenMode, evolisConfig.ErrorManagement),
      p.IsDeleted, p.CreatedAt, p.UpdatedAt, p.DeletedAt);

        /// <summary>
        /// Enforces decision Q-15: a Matica printer must carry a Matica configuration and no
        /// Evolis one; an Evolis printer must carry an Evolis configuration and no Matica one.
        /// Shared by <see cref="CreateAsync"/> and <see cref="UpdateAsync"/> so the rule can
        /// never drift between the two.
        /// </summary>
        private static Result ValidatePrinterConfigPayloadShape(
            UsingPrinterType usingPrinterType,
            MaticaPrinterConfigRequest? maticaConfig,
            EvolisPrinterConfigRequest? evolisConfig)
        {
            if (usingPrinterType == UsingPrinterType.Matica)
            {
                if (maticaConfig is null) return Result.Failure(PrintingErrors.MaticaPrinterConfigRequired());
                if (evolisConfig is not null) return Result.Failure(PrintingErrors.EvolisPrinterConfigNotApplicable());
                return Result.Success();
            }

            if (usingPrinterType == UsingPrinterType.Evolis)
            {
                if (evolisConfig is null) return Result.Failure(PrintingErrors.EvolisPrinterConfigRequired());
                if (maticaConfig is not null) return Result.Failure(PrintingErrors.MaticaPrinterConfigNotApplicable());
                return Result.Success();
            }

            return Result.Success();
        }

        /// <summary>Device name is the SDK connection key (Evolis.Device.name) — required, trimmed, ≤ 200 chars.</summary>
        private static Result ValidateDeviceName(string deviceName)
        {
            string trimmed = deviceName?.Trim() ?? string.Empty;
            return trimmed.Length is 0 or > 200
                ? Result.Failure(PrintingErrors.DeviceNameRequired())
                : Result.Success();
        }
    }
}
