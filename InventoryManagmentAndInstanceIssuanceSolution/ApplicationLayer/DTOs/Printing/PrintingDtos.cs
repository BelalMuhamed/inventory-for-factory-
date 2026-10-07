using System;
using System.Collections.Generic;
using ApplicationLayer.DTOs.Products;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Printing
{
    // =====================================================================================
    //  Printers (ERD §6, Printing Module Q-01/Q-09)
    // =====================================================================================

    /// <summary>
    /// Matica-only machine configuration, nested under a printer payload (ERD §6.2). The four
    /// tipper fields are printer-level hardware calibration (Matica Print Flow, tipper-parameter
    /// phase) — optional, defaulting to 0 to match <see cref="DomainLayer.Entities.MaticaPrinterConfiguration"/>'s
    /// own default-0 behavior for a printer that has never had them explicitly configured.
    /// </summary>
    public sealed record MaticaPrinterConfigRequest(
        int FeederId, int HopperId, int RejectedId, string Port, string IpAddress,
        int TipperTemperature = 0, int TipperPressure = 0, int TipperConsumption = 0, int TipperTime = 0);

    /// <summary>Matica-only machine configuration as returned to clients (ERD §6.2).</summary>
    public sealed record MaticaPrinterConfigResponse(
        int FeederId, int HopperId, int RejectedId, string Port, string IpAddress,
        int TipperTemperature, int TipperPressure, int TipperConsumption, int TipperTime);

    /// <summary>
    /// Create payload for <c>POST /api/printers</c> (Printing Module Q-01/Q-09: system-admin
    /// only). <paramref name="MaticaConfig"/> is required when <paramref name="UsingPrinterType"/>
    /// is <see cref="UsingPrinterType.Matica"/> and must be absent when it is
    /// <see cref="UsingPrinterType.Evolis"/> — Evolis needs no server-side machine configuration
    /// (module requirement §1).
    /// </summary>
    /// <param name="TenantId">
    /// Used only by a system-admin caller to target a tenant; for a tenant caller it is ignored
    /// (the token's tenant is used), mirroring <c>CreateProductRequest.TenantId</c>. In practice
    /// every write to this resource is system-admin-only (decision Q-09), so this is normally
    /// how the target tenant is supplied.
    /// </param>
    public sealed record CreatePrinterRequest(
       long BranchId,
       UsingPrinterType UsingPrinterType,
       string Name,
       string Model,
       string UniqueNumber,
       MaticaPrinterConfigRequest? MaticaConfig,
       EvolisPrinterConfigRequest? EvolisConfig,
       long? TenantId = null);

    /// <summary>
    /// Update payload for <c>PUT /api/printers/{id}</c> (decision Q-09: system-admin only).
    /// <see cref="UsingPrinterType"/> is deliberately not present — a physical printer's family
    /// is a hardware fact and does not change after registration, unlike a product's printer
    /// family (Q-08), which is a business choice about which machine prints it.
    /// </summary>
    public sealed record UpdatePrinterRequest(
        long BranchId,
        string Name,
        string Model,
        string UniqueNumber,
        MaticaPrinterConfigRequest? MaticaConfig,
        EvolisPrinterConfigRequest? EvolisConfig);

    /// <summary>Printer projection returned to clients, including its Matica extension when applicable.</summary>
    public sealed record PrinterResponse(
        long Id,
        long TenantId,
        long BranchId,
        string BranchName,
        UsingPrinterType UsingPrinterType,
        string Name,
        string Model,
        string UniqueNumber,
        MaticaPrinterConfigResponse? MaticaConfig,
        EvolisPrinterConfigResponse? EvolisConfig,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        DateTime? DeletedAt);

    /// <summary>
    /// Filter/paging inputs for <c>GET /api/printers</c> (decision Q-09: tenant users get
    /// read-only access filtered by type and branch).
    /// </summary>
    /// <param name="UsingPrinterType">Optional printer-family filter; null matches both.</param>
    /// <param name="BranchId">Optional branch filter.</param>
    /// <param name="UniqueNumber">
    /// Optional exact-match filter on the printer's serial (Evolis) or IP address (Matica) —
    /// added for the Matica Print Flow, so a printer can be resolved by its known IP instead of
    /// paging through a branch's full printer list client-side. Exact match, not substring, since
    /// this is an identity lookup (find <em>this specific</em> device), not a browsing filter.
    /// </param>
    /// <param name="IsDeleted">Optional soft-delete filter; null matches both.</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100 per spec). Defaults to 20.</param>
    /// <param name="SortBy">Optional sort field; mapped to a whitelisted column in the repository.</param>
    /// <param name="SortDir">Sort direction, "asc" or "desc". Defaults to "asc".</param>
    public sealed record PrinterListFilter(
        UsingPrinterType? UsingPrinterType = null,
        long? BranchId = null,
        string? UniqueNumber = null,
        bool? IsDeleted = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");

    // =====================================================================================
    //  Ribbon types (Printing Module Q-05)
    // =====================================================================================

    /// <summary>A ribbon type an Evolis configuration can reference.</summary>
    /// <summary>A ribbon type an Evolis configuration can reference.</summary>
    public sealed record RibbonTypeResponse(long Id, string Name, string SdkCode);
    // =====================================================================================
    //  Product print configuration (ERD §7, Printing Module Q-02/Q-03/Q-04/Q-05/Q-07/Q-08/Q-09)
    // =====================================================================================

    /// <summary>Matica printing parameters payload (decision Q-03: no separate Font/FontFamily field).</summary>
    public sealed record MaticaPrintConfigRequest(
        int Cpi,
        int FontSize,
        int OffsetX,
        int OffsetY,
        long? ImageId);

    /// <summary>Matica printing parameters as returned to clients.</summary>
    public sealed record MaticaPrintConfigResponse(
        int Cpi,
        int FontSize,
        int OffsetX,
        int OffsetY,
        long? ImageId);

    /// <summary>Evolis printing parameters payload. <see cref="RibbonTypeId"/> per decision Q-05.</summary>
    public sealed record EvolisPrintConfigRequest(
        long RibbonTypeId,
        PrintWay PrintWay,
        int X,
        int Y,
        PrintedFace PrintedFace,
        string FontFamily,
        int FontSize,
        string PrintColor,
        string BackgroundColor,
        string FontStyle,
        long? ImageId);

    /// <summary>Evolis printing parameters as returned to clients, with the ribbon type resolved by name.</summary>
    public sealed record EvolisPrintConfigResponse(
        long RibbonTypeId,
        string RibbonTypeName,
        PrintWay PrintWay,
        int X,
        int Y,
        PrintedFace PrintedFace,
        string FontFamily,
        int FontSize,
        string PrintColor,
        string BackgroundColor,
        string FontStyle,
        long? ImageId);

    /// <summary>
    /// Payload for <c>PUT /api/products/{id}/print-config</c> (decision Q-07: sub-resource, no
    /// standalone POST/DELETE). Also the payload shape for a printer-family switch (decision
    /// Q-08): supplying a different <see cref="UsingPrinterType"/> than the product currently has,
    /// together with that family's payload, switches it — the old configuration row is hard
    /// deleted, not soft deleted, in the same transaction as the new row's insert.
    /// </summary>
    public sealed record UpdateProductPrintConfigRequest(
        UsingPrinterType UsingPrinterType,
        MaticaPrintConfigRequest? Matica,
        EvolisPrintConfigRequest? Evolis);

    /// <summary>
    /// Response for <c>GET</c>/<c>PUT /api/products/{id}/print-config</c>. Exactly one of
    /// <see cref="Matica"/> / <see cref="Evolis"/> is populated, matching
    /// <see cref="UsingPrinterType"/> — the client does not need to guess which one to read.
    /// </summary>
    public sealed record ProductPrintConfigResponse(
        long ProductId,
        UsingPrinterType UsingPrinterType,
        MaticaPrintConfigResponse? Matica,
        EvolisPrintConfigResponse? Evolis);

    /// <summary>
    /// Combined product + print-configuration view for
    /// <c>GET /api/products/{id}/print-config/full</c> (Printing Module, phase 7). System-admin
    /// only. <see cref="PrintConfig"/> is <c>null</c> when the product has no configuration yet —
    /// this endpoint surfaces that gap rather than failing, since it exists specifically as an
    /// administrative overview.
    /// </summary>
    public sealed record ProductWithPrintConfigResponse(
        ProductResponse Product,
        ProductPrintConfigResponse? PrintConfig);

    // =====================================================================================
    //  Print images (module requirements §5/§6/§7; revised for admin-only upload, ImageId
    //  references, and explicit replace)
    // =====================================================================================

    /// <summary>
    /// Print image metadata returned to clients. Deliberately excludes
    /// <c>StoredFileName</c>/<c>StoredPath</c> — physical layout is an implementation detail;
    /// clients retrieve content via <c>GET /api/print-images/{id}</c>, never by constructing a
    /// path themselves.
    /// </summary>
    public sealed record PrintImageResponse(
        long Id,
        long TenantId,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        DateTime UploadedAt);

    /// <summary>
    /// Result of <c>POST /api/print-images</c>. <paramref name="Created"/> is <c>false</c> when a
    /// non-deleted image with the same original file name already existed for the target tenant —
    /// nothing was saved, and <paramref name="Image"/> is that <em>existing</em> image's metadata,
    /// not a new one. The controller uses this to choose between <c>200 OK</c> (created) and
    /// <c>409 Conflict</c> (already exists) — the client decides whether to keep the existing
    /// image (do nothing further) or replace it explicitly via
    /// <c>PUT /api/print-images/{id}</c> using the id in <paramref name="Image"/>.
    /// </summary>
    public sealed record UploadPrintImageResult(bool Created, PrintImageResponse Image);

    /// <summary>
    /// Summary of a run of the one-time legacy-image migration (renaming GUID-named,
    /// tenant-id-foldered files to the current original-name/tenant-username scheme). Idempotent:
    /// re-running finds nothing left to migrate once every row has been processed once.
    /// </summary>
    /// <param name="Migrated">Rows successfully moved to the new naming scheme this run.</param>
    /// <param name="AlreadyCurrent">Rows that already matched the new scheme — nothing to do.</param>
    /// <param name="Failed">Rows that could not be migrated (see <paramref name="Notes"/> for why).</param>
    /// <param name="Notes">Human-readable detail per row that was renamed to avoid a collision, or that failed.</param>
    public sealed record MigrateLegacyImagesResult(
        int Migrated, int AlreadyCurrent, int Failed, IReadOnlyList<string> Notes);

    /// <summary>
    /// Evolis-only machine configuration, nested under a printer payload (Evolis Integration
    /// Plan §6). <see cref="DeviceName"/> is the SDK connection key and is the only field
    /// without a sensible default. <see cref="SupervisorHostName"/> is deliberately absent —
    /// it is never accepted from this payload, only written by Phase 2's self-registration route.
    /// </summary>
    public sealed record EvolisPrinterConfigRequest(
        string DeviceName,
        long? DefaultRibbonTypeId = null,
        MagCoercivity MagCoercivity = MagCoercivity.PRINTER,
        InputTray InputTray = InputTray.FEEDER,
        OutputTray OutputTray = OutputTray.STANDARD,
        OutputTray ErrorTray = OutputTray.ERROR,
        EvolisOpenMode OpenMode = EvolisOpenMode.SUPERVISED,
        EvolisErrorManagement ErrorManagement = EvolisErrorManagement.SOFTWARE);

    /// <summary>Evolis-only machine configuration as returned to clients.</summary>
    public sealed record EvolisPrinterConfigResponse(
        string DeviceName,
        string? SupervisorHostName,
        DateTime? SupervisorRegisteredAt,
        long? DefaultRibbonTypeId,
        MagCoercivity MagCoercivity,
        InputTray InputTray,
        OutputTray OutputTray,
        OutputTray ErrorTray,
        EvolisOpenMode OpenMode,
        EvolisErrorManagement ErrorManagement);

    // =====================================================================================
    //  Supervisor self-registration (Evolis Integration Plan §7, decisions Q-21/Q-28)
    // =====================================================================================

    /// <summary>
    /// One Evolis device the Agent's own discovery call (<c>Evolis.GetDevices()</c>) actually
    /// found on the supervising machine, reported alongside a supervisor-registration request.
    /// Only <see cref="DeviceName"/> is used for matching; the rest is diagnostic context the API
    /// deliberately does not persist (Phase 1's "deliberately NOT stored" list), since it is
    /// runtime-discoverable and would go stale the moment it's written.
    /// </summary>
    public sealed record DiscoveredDeviceDto(
        string DeviceName, string? Model, string? Link, string? DriverVersion, bool IsSupervised);

    /// <summary>
    /// Payload for <c>POST /api/printers/supervisor-registration</c>, sent by the Printer Agent
    /// under its reconciliation service-account token (Q-28) — never by Angular or a tenant/admin
    /// session. <see cref="BranchId"/> must match the authenticated token's own <c>branchId</c>
    /// claim; the controller enforces this the same way <c>PrintFlowController</c> already does
    /// for its own Agent-only actions.
    /// </summary>
    public sealed record SupervisorRegistrationRequest(
        long BranchId, string HostName, IReadOnlyList<DiscoveredDeviceDto> Devices);

    /// <summary>One registered Evolis printer the Agent's report matched by <c>(TenantId, BranchId, DeviceName)</c>.</summary>
    public sealed record MatchedSupervisorDevice(long PrinterId, string DeviceName, bool Updated);

    /// <summary>
    /// A device the Agent's discovery call found but which has no corresponding registered Evolis
    /// printer in this branch — reported, never auto-created (module requirement: printer
    /// registration stays an explicit admin action).
    /// </summary>
    public sealed record UnmatchedDiscoveredDevice(string DeviceName, string Reason);

    /// <summary>A registered Evolis printer in this branch that the Agent's report did not include.</summary>
    public sealed record UnseenRegisteredPrinter(long PrinterId, string DeviceName);

    /// <summary>
    /// Reconciliation result for one supervisor-registration call — the three-way partition that
    /// makes this endpoint useful to the Agent rather than fire-and-forget.
    /// </summary>
    public sealed record SupervisorRegistrationResponse(
        IReadOnlyList<MatchedSupervisorDevice> Matched,
        IReadOnlyList<UnmatchedDiscoveredDevice> Unmatched,
        IReadOnlyList<UnseenRegisteredPrinter> RegisteredButNotDiscovered);
}
