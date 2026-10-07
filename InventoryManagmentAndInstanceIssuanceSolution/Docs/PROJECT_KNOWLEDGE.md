# Project Knowledge

> Living technical documentation for this repository.
> This file must be updated whenever project behavior, architecture,
> database structure, APIs, integrations, or important technical decisions change.

## AI Agent Instructions

Before modifying the project:

1. Read this file.
2. Inspect the relevant code — always re-clone/re-read from disk; never work from a prior summary or this file alone.
3. Treat the code as the source of truth. Where this file and the code disagree, the code wins — fix this file, not your understanding of the code.
4. Identify affected modules and invariants (§4, §11).
5. Update this file when the change affects documented knowledge (see §"Automatic Maintenance Rule" at the end).

Do not assume this document is complete. Several areas are marked `⚠️ Unclear / requires confirmation` — verify those against the code before relying on them.

---

## 1. Project Overview

**Purpose:** A multi-tenant inventory management and card issuance platform ("calimly"). Tenants (banks) manage a catalog of card products, receive physical cards via encrypted batch upload, track per-branch stock, transfer cards between their own branches, dispose of written-off cards, generate encrypted card files for printers, and raise/fulfil inter-branch stock requests.

**Main business responsibilities:**
- Tenant & branch management (auth identity = tenant account, no separate users table)
- Product catalog (card types / SKUs)
- Batch card ingestion (encrypted `.dat` file upload → parse → validate → persist `ProductItem` rows)
- Stock tracking per (tenant, branch, product) with available/hold quantities
- Card transfers between branches, with partial-receipt/dispose/return lifecycle
- Branch stock requests (a branch asks for more of a product; another branch's transfer(s) fulfil it)
- Card write-off (disposal) with mandatory reason
- Encrypted card-file generation for print integration
- Audit logging of all CRUD and key non-CRUD actions

**Main applications:** Single ASP.NET Core Web API (no separate front-end in this repo).

**Main technologies:** .NET 8, ASP.NET Core, EF Core 8.0.27, SQL Server, Serilog (with AES-encrypted file sinks), ClosedXML (Excel failed-rows reports), Swashbuckle 6.6.2, JWT bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`), PBKDF2 via `Microsoft.Extensions.Identity.Core`.

**Runtime/framework versions:** All four projects target `net8.0`. The API Endpoint Specification document references .NET 10 — the codebase has **not** been retargeted (see README, confirmed still accurate). Nullable reference types and implicit usings are enabled everywhere.

**Important external dependencies:** SQL Server only. No message broker, no cache, no cloud storage, no third-party HTTP integrations exist in this codebase today. No background job runner (Hangfire etc.) — batch upload runs synchronously inside the request.

---

## 2. Repository Structure

```
InventoryManagmentAndInstanceIssuanceSolution.sln
├── DomainLayer/                                              # Zero dependencies
│   ├── Common/          Result, Result<T>, Error, ErrorCategory, AuditableEntity
│   ├── Entities/         All EF entities (POCOs, no EF Core reference)
│   └── Enums/            All persisted enums (TINYINT via HasConversion<byte>())
├── ApplicationLayer/                                          # Depends on Domain only
│   ├── common/           PaginatedResponse<T>
│   ├── Contracts/        IUnitOfWork, IGenericRepo<T,TKey>, named repo interfaces,
│   │                     ICurrentTenant, security/crypto contracts, ITransferComposer
│   ├── DTOs/              Request/response DTOs, grouped by module folder
│   ├── Errors/            Static *Errors classes — one per module, all Error factories
│   ├── Options/           Strongly-typed config sections (Options pattern)
│   ├── ServicesContracts/ IServiceManager + one interface per service
│   ├── BatchUpload/       .dat wire-format spec, row parser, failure-reason model
│   └── CardFiles/         Card-file line/writer contracts, rejection reasons
├── InfrastructureLayer/                                       # Depends on Application + Domain
│   ├── Data/              AppDbContext (all Fluent API config), DbSeeder, SaveChanges interceptor
│   ├── Migrations/        EF Core migrations (committed as source, 16 as of R1-BranchRequests)
│   ├── Repositories/      One repo class per aggregate, implementing the named-method contracts
│   ├── Security/          JwtTokenGenerator, Pbkdf2PasswordHasher, PanFingerprintGenerator, BatchFileCipher
│   ├── Services/          One service class per ServicesContracts interface + ServiceManager facade
│   ├── Reporting/         FailedRowsReportBuilder (ClosedXML)
│   ├── Logging/           Serilog encrypted file sink + options
│   └── UnitOfWork.cs      IUnitOfWork implementation (repo aggregation + ExecuteInTransactionAsync)
└── InventoryManagmentAndInstanceIssuancePresentationLayer/     # Depends on Application only
    ├── Controllers/        One controller per resource (see §6)
    ├── Common/              ApiResponse<T>, ApiError, ResultExtensions (Result → IActionResult), ValidationResponseFactory
    ├── MiddleWares/         GlobalExceptionMiddleware
    ├── Security/             AuthorizationPolicies, CurrentTenant (ICurrentTenant impl from JWT claims)
    ├── Filters/              LocalizeErrorResultFilter (Error.Code → IStringLocalizer message)
    ├── Swagger/              AcceptLanguageHeaderOperationFilter, ExamplesOperationFilter,
    │                         EnumSchemaDescriptionsFilter, ExampleCatalog, Examples/*.cs — one
    │                         file per module, one per controller, all 15 controllers covered as
    │                         of Phase S5 (§3, "Swagger / API documentation")
    ├── Resources/Localization/ Messages.resx (EN) + Messages.ar.resx (AR), Messages.cs bundle
    └── Program.cs            Composition root
```

Root of the repo also holds a sequence of `git format-patch` files (`R1..R6-*.patch`, `T0..T6-*.patch`, `phase1..phase9-*.patch`, etc.) — these are the delivery artifacts for each implementation phase, already applied to `main`. They are historical delivery records, not living documentation; do not treat their presence as meaning the corresponding work is unapplied.

---

## 3. Architecture

**Style:** Strict Onion / Clean Architecture. Dependency direction: `Presentation → Application ← Infrastructure`, with `Domain` at the center depended on by everything. Infrastructure is never referenced from Domain or Application. The Presentation layer is the composition root (`Program.cs`) and is the only place `InfrastructureServiceRegistration.AddInfrastructure` is called from.

**Key architectural patterns:**

| Pattern | Where | Notes |
|---|---|---|
| Result monad | `DomainLayer.Common.Result` / `Result<T>` | No thrown exceptions for business outcomes. `Result`'s constructor throws `InvalidOperationException` if success/error are inconsistent — a correctness guard, not a business-flow exception. |
| Response envelope | `ApiResponse<T>` (Presentation) | `{ success, data, error }` on every endpoint, success or failure. `ResultExtensions.ToActionResult(this)` is the one-liner controller convention. |
| Error → HTTP mapping | `ErrorCategory` enum | `Validation`→422, `Conflict`→409, `NotFound`→404, `Unauthorized`→401, `Forbidden`→403, `Internal`→500 (introduced for batch-upload's own boundary catch). |
| Repository + Unit of Work | `IGenericRepo<T,TKey>` + named repo interfaces, `IUnitOfWork` | Raw query predicates never appear in services; every query is a named repo method. Repos share one `AppDbContext` per request/UoW instance. |
| Multi-tenancy | `ICurrentTenant` (claims-derived) | **Not** enforced via global EF query filters for tenant — only soft-delete has a global filter (`!IsDeleted`) on `AuditableEntity`-derived types with one. Tenant scoping is manual: every repo method that should be tenant-scoped takes an explicit tenant id/scope parameter. `AuditSaveChangesInterceptor` and `AppDbContext` constructor both take `ICurrentTenant` for cross-cutting behavior (audit rows, no query filter). |
| Service façade | `IServiceManager` | One property per service interface; controllers depend on `IServiceManager` only, not individual services. |
| Lazy factory pattern | `System.Func<IXxxService>` registrations in `InfrastructureServiceRegistration` | Registered alongside most services (e.g. `services.AddScoped<Func<IBranchService>>(...)`) — ⚠️ Unclear/requires confirmation: no `ServiceManager.cs` consumer of these `Func<>` registrations was inspected in this pass; verify whether `ServiceManager` actually injects `Lazy<T>`/`Func<T>` or resolves services directly before relying on this pattern in new code. |
| Concurrency control | `[Timestamp] byte[] RowVersion` | On `CardTransfer`, `BranchRequest`, `Stock`. `DbUpdateConcurrencyException` is caught at both the service layer (mapped to a module-specific `ConcurrencyConflict()` error, 409) and, as a last-resort net, in `GlobalExceptionMiddleware` (also 409). |
| Enum persistence | `.HasConversion<byte>()` in Fluent API, on every persisted enum | Enum values documented in each enum's XML doc as "ERD value N"; new values are always appended, never renumbered, to avoid silent remapping of already-persisted rows. |
| Cross-aggregate FKs | `DeleteBehavior.NoAction` | Applied on essentially every FK that isn't a strict parent→child ownership edge, specifically to avoid SQL Server's multiple-cascade-path error and to keep movement/audit history intact even if a branch/product is later deleted. |
| Cascade FKs | `DeleteBehavior.Cascade` | Only on true parent-owns-child edges: `Batch→ProductItem`, `CardTransfer→{Products,Items}`, `CardDisposal→Items`, `BranchRequest→Items`. |
| Controller shape | One-liner actions | `(await _services.X.YAsync(...)).ToActionResult(this)` — no logic in controllers. |
| Validate-then-write | Services validate every input line before any write | e.g. `BranchRequestService.CreateAsync` loads/validates every product line before constructing the entity; `ConfirmAsync` calls `ITransferComposer.ValidateAsync` for every plan before any `StageAsync` runs inside the transaction. |
| Shared core extraction | `ITransferComposer` | `TransferService.CreateAsync`'s original logic split into `ValidateAsync` (read-only) / `StageAsync` (writes, must run inside an ambient transaction) so `BranchRequestService.ConfirmAsync` can validate every plan up front, then stage them all inside one transaction. |

**Cross-cutting concerns:**
- **Structured logging:** Serilog, writing to console plus two AES-encrypted file sinks (exceptions; warnings+ without exception). Requires `LogEncryption:Password` / `:Salt` via user-secrets/env — app fails to start (in `Program.cs`, before the host is even built) if missing.
- **Global exception handling:** `GlobalExceptionMiddleware`, registered first in the pipeline (before Swagger/auth), maps `DbUpdateConcurrencyException`→409 and everything else→500, always via the `ApiResponse<object>` envelope, and stamps `X-Trace-Id` on every response (correlates with `HttpContext.TraceIdentifier`, which Serilog also logs).
- **Model validation:** `ApiBehaviorOptions.InvalidModelStateResponseFactory` replaced with `ValidationResponseFactory.Build`, so `[ApiController]`'s automatic 400 becomes a 422 in the same envelope shape as business validation failures.
- **Localization:** `LocalizeErrorResultFilter` (an `IAsyncResultFilter`, registered as `options.Filters.Add<LocalizeErrorResultFilter>()`) rewrites `ApiError.Message` by looking up `Error.Code` in `IStringLocalizer`-backed `Messages.resx`/`Messages.ar.resx`, using the incoming `Accept-Language` header (`app.UseRequestLocalization()`).
- **Audit logging:** Two mechanisms, not one — (1) `AuditSaveChangesInterceptor` (EF `SaveChangesInterceptor`) automatically writes one `AuditLog` row per `Added`/`Modified`/`Deleted` change to any `AuditableEntity`-derived type, discovering the owning tenant by convention off a `TenantId` property; (2) `IAuditLogger.StageAction(...)`, called explicitly by services for non-CRUD or non-`AuditableEntity` actions (Login, branch-request Confirm/Refuse/Cancel, transfer creation/settlement, disposal) since those aggregates (`CardTransfer`, `BranchRequest`, `CardDisposal`, etc.) are deliberately **not** `AuditableEntity`.
- **Swagger / API documentation (added Phase S1, 2026-08-12):** No new NuGet package — extends the existing hand-written-`IOperationFilter` pattern (`AcceptLanguageHeaderOperationFilter` was already doing this). Three pieces, all in `Presentation/Swagger/`:
  - `IncludeXmlComments` is now wired for all three XML-doc-producing assemblies (Presentation, ApplicationLayer, DomainLayer — the latter two had `GenerateDocumentationFile` enabled for the first time in this pass). Every layer's existing `<summary>`/`<param>`/`<response>` doc comments now reach Swagger UI; previously none of them did, regardless of how complete they were in source.
  - `EnumSchemaDescriptionsFilter` (`ISchemaFilter`) appends each enum's numeric wire value + member doc to its schema description, reading the same XML doc files via `XmlDocIndex` — needed because no `JsonStringEnumConverter` is registered anywhere (enums serialize as plain integers; see §12 enum persistence decision) and Swashbuckle's built-in XML support documents the enum type, not each member.
  - `ExamplesOperationFilter` attaches named, multi-scenario request/response examples per action, looked up from `ExampleCatalog.All` by `(Controller, Action)`. **To extend for a new controller:** add one `Swagger/Examples/{Module}Examples.cs` file (mirrors `Errors/{Module}Errors.cs` — one file per module) building an `EndpointExampleSet` per action via `EndpointExampleSetBuilder`, then add one `Merge(catalog, {Module}Examples.Build());` line in `ExampleCatalog.Build()`. Examples are plain C# objects (anonymous types), not hand-built `IOpenApiAny` graphs — `OpenApiAnyFactory` does that conversion.
  - **Coverage: complete as of this pass (Phase S5).** Foundation (XML comments, enum filter, examples filter/catalog) plus full example content now cover all 15 controllers / ~90 endpoints: `AuthController`, `TenantsController` (S1); `BranchController`, `ProductController`, `ProductPrintConfigController`, `PrintersController`, `PrintImagesController` (S2); `StockController`, `ProductItemsController`, `InventoryController` (batch upload), `InventoryHistoryController`, `CardFilesController` (S3); `TransactionsController`, `CardDisposalsController` (S4); `BranchRequestsController` (S5). To extend for future new endpoints: add the controller's docs comments per the pattern above, add one `Swagger/Examples/{Module}Examples.cs`, and add one `Merge(catalog, {Module}Examples.Build());` line in `ExampleCatalog.Build()` — same one-module-per-file convention throughout.
  - **A genuine finding from this pass, not a bug fix:** `[Authorize]`/`[Authorize(Policy=...)]` failures (missing/invalid token, wrong policy) are handled by ASP.NET Core's authorization middleware *before* an action runs, and return an **empty body** — not the `ApiResponse<T>` envelope. Corrected on every controller (S1-S5). The rare enveloped-401 edge case (`*.ActorNotResolved`) recurs throughout — reachable on most controllers, but confirmed *unreachable* on `TransactionsController` and `BranchRequestsController` specifically (both documented as such per their own doc comments, rather than given a misleading "reachable" example).
  - **A second finding, S2:** the Printing module's admin-only write actions enforce "system-admin only" in the *service* (`Forbidden` category), not via an authorization policy — so their 403 genuinely *is* the standard enveloped `ApiResponse<T>` body, unlike `TenantsController`'s policy-driven empty-body 403. The same pattern recurs on `TransactionsController` (Create/Receive/Dispose), `CardDisposalsController.Dispose`, and `BranchRequestsController` (Create/Confirm/Refuse/Cancel) — every one of them rejects a system-admin token the same enveloped way (`*.SystemAdminNotAllowed`).
  - **A third finding, S2:** `PrintImagesController.Upload`'s `409 Conflict` is not a failure envelope at all — it returns a normal success envelope (the existing image's metadata) with the status forced to 409. Still the only endpoint found with that specific shape.
  - **A finding from S3:** `CardFilesController.Generate`'s success response (200) is a raw binary file, not JSON — hand-off metadata travels in response headers instead. `StockController.GetByBranch` never 404s on an unknown branch (confirmed by reading `StockService`) — it returns an empty page.
  - **A finding from S4, and a real doc-comment bug caught and fixed:** `CardDisposalsController`'s class-level doc previously claimed "unlike `TransactionsController`, there is no read-only admin path here at all" — false. `DisposalService.ResolveReadScope` confirms `GetAll`/`GetById` give a system admin the same cross-tenant read access `TransactionsController` has; only `Dispose` (the write path) rejects an admin token. `TransferErrors.cs` (30+ codes) and `BranchRequestErrors.cs` are large enough that Swagger examples cover only the most illustrative scenario per response code, not an exhaustive enumeration — the source files remain authoritative for the full catalogues.
  - **A finding from S5:** `BranchRequestsController.Confirm` composes `TransferErrors`/`StockErrors` directly for anything specific to a generated transfer (Known/Unknown item-id shape, insufficient stock) — a confirm hitting one of those surfaces the exact same code a direct `TransactionsController.Create` would, by design (`ITransferComposer` is the single shared validation path). Its own examples therefore mix `BranchRequest.*` codes (request-specific) with a reused `Transfer.*` code (generated-transfer-specific) in the same response set, documented as such rather than pretending every failure has its own `BranchRequest`-prefixed code.

---

## 4. Domain / Business Modules

### Auth & Tenancy
- **Responsibility:** Authentication (tenant + system admin), tenant CRUD, branch CRUD.
- **Main entities:** `Tenant` (is itself the auth identity — no separate users table; one account per tenant), `SystemAdmin` (bootstrap admin, outside the tenant model), `RefreshToken`, `Branch`.
- **Key rule:** Single account per tenant. JWT claims: `username`, `isSystemAdmin` (`"true"`/`"false"` string), `tenantId` (present only for tenant tokens). `[Authorize]` only — no permission/role attributes anywhere; the one exception is the `SystemAdminOnly` policy (`AuthorizationPolicies`), used to gate the tenant-management module to system admins.
- **Constraint:** `SystemAdmin` never performs regular tenant operations (create transfer, confirm branch request, dispose cards) — every write-path service rejects an admin token outright with a module-specific `SystemAdminNotAllowed`/equivalent error; admin access elsewhere is read-only across tenants.

### Catalog (Products, Branches)
- **Responsibility:** Tenant's card-type catalog and branch list.
- **Main entities:** `Product` (name unique per tenant among non-deleted rows; `ProductTransactionWay` — see Transfers below — and `UsingPrinterType`), `Branch` (name unique per tenant among non-deleted rows; `IsActive` flag).
- **Invariant:** `Product.ProductTransactionWay` (Known vs Unknown) is **immutable once cards exist for that product** (decision P6) — settlement logic depends on this never drifting after cards are in flight.

### Stock & Cards
- **Responsibility:** Per-(tenant, branch, product) quantity tracking; individual physical card records.
- **Main entities:** `Stock` (composite PK `(TenantId, BranchId, ProductId)`; `AvailableQuantity` + `HoldQuantity`, both `>= 0` via check constraints), `ProductItem` (one row per physical card; `PanFingerprint` is the sole identity/dedup key, `MaskedPan` is display-only).
- **Key invariant (Known-way products):** `count(ProductItems WHERE BranchID=X AND Status=Available AND ProductId=P) == Stock[X,P].AvailableQuantity`.
- **Key invariant (Unknown-way products):** that equality does **not** hold — `Stock` is the branch's entitlement; backing cards live in a tenant-wide unassigned pool (`ProductItem.BranchID IS NULL`, `Status = Available`), and which pool card backs which branch is resolved only at print time.
- **`ProductItem.BranchID == null`** means exactly two things, with two different statuses: in-transit under an in-flight transfer (`Status = OnHold`), or an Unknown-way card received-but-not-yet-printed (`Status = Available` — `OnHold` is reserved for the in-transit case, not for a card merely awaiting its first print). A null-branch card is immutable to everything outside the Transactions module (batch re-sight, status update, soft delete all refuse to touch it).

### Batch Upload
- **Responsibility:** Ingest AES-256-GCM-encrypted `.dat` files of cards into `ProductItem` rows.
- **Wire format (`ApplicationLayer.BatchUpload.BatchFileFormat`):** one card per line, `PAN|ProductName|BranchName`, UTF-8, **no BOM** (load-bearing — decryption doesn't strip a preamble), `\n` emitted (parser accepts `\r\n` too). PAN: 13–19 digits, Luhn-valid.
- **Main entities:** `Batch` (one row per upload run; `FileMac` = SHA-256 of decrypted content, unique per tenant among non-deleted rows — the duplicate-file guard).
- **Key rule:** The pipeline runs **synchronously inside one request/transaction** — no queued/async state, hence `UploadStatus` has no Pending/Processing value.
- **Known intentional gap:** `BatchUploadService` does **not** mark a `Batch` row `Failed` on an unhandled exception mid-pipeline, to avoid permanently blocking re-upload after a transient failure (the `FileMac` uniqueness constraint would otherwise block retry of the same file).
- **Secrets:** `BatchCipherOptions` (`MasterSecret` + `Salt`) — separate secret from `PanHashOptions` by design (key separation). Both fail-fast at startup via `ValidateOnStart()`.

### Card File Generation
- **Responsibility:** Produce an encrypted `.dat`-format file (same wire format as batch upload, produced by `CardFileWriter`) for printer integration.
- **Config:** `CardFileOptions.MaxCardsPerRequest` (default 50,000) — caps unbounded in-memory plaintext/base64 construction per request.
- **Errors:** `CardFileErrors` supports field-level rejection detail via `Error.WithDetails(...)` (added specifically so a rejected generation request can report *which* cards failed and why).

### Transfers (Card Transfers)
- **Responsibility:** Move cards between two branches of the same tenant; settle via a single `receive` call carrying a per-line disposition (no separate `refuse` endpoint).
- **Main entities:** `CardTransfer` (append-only — no `AuditableEntity`, no soft delete, no query filter; own `CreatedAt`/`RowVersion`), `CardTransferProduct` (one row per product line, snapshots `ProductTransactionWay` at creation), `CardTransferItem` (one row per physical card, **Known-way lines only** — an Unknown-way line moves `Stock` entitlement alone and has no card to select, so it carries no item rows at all, before or after settlement).
- **Maker-Checker identity (Unknown-way Maker-Checker workflow, decision confirmed with the repo owner):** `CardTransfer.CreatedByUsername` (required) and `.CheckedByUsername` (nullable, set at settlement) record the acting account on every transfer, Known-way or Unknown-way. It is explicitly fine for the same account to be both Maker and Checker — this system has one account per tenant, so there is no per-user identity to compare; what the workflow requires is that identity be *recorded*, not that two different identities be proven.
- **Two ways a transfer is born:** `TransactionOrigin.UserCreated` (direct create, or via a confirmed `BranchRequest`) or `TransactionOrigin.AutoGeneratedReturn` (system-manufactured to carry a partial receipt's unreceived remainder back to the source; `ParentTransferId` set; chains are **not** capped at one level — `Parent.Parent` can be non-null).
- **Settlement identity (DB-enforced check constraint):** `RealQuantityReceived + DisposedQuantity <= TransactedQuantity` per line; the returned remainder is `TransactedQuantity - RealQuantityReceived - DisposedQuantity`, deliberately **not stored** (derivable, so storing it invites disagreement).
- **Known-way settlement (current state, ⚠️ open design point per Belal's plan):** every card on a Known-way line must be given an explicit disposition (`Received`/`Disposed`/`NotReceived`) in the `receive` call — `TransferService` currently **requires** `ItemDispositions` for every item on a Known-way line (`TransferErrors.DispositionsRequired` / `DispositionCountMismatch` if missing or mismatched).
- **Unknown-way settlement (Unknown-way Maker-Checker workflow — supersedes the earlier "Unknown Inventory Refactor," which settled these lines immediately at creation/confirm):** an Unknown-way line now follows the same create → pending → settle lifecycle a Known-way line always had, just without any card to select. At creation (`TransferComposer.StageAsync`), the line moves the source's `Available → Hold` and leaves the target untouched — `RealQuantityReceived` stays `null` (pending), and the transfer always opens `InProgress` (there is no longer a case where staging closes a transfer outright, Known-way or Unknown-way). At settlement (`TransferService.SettleAsync`), the caller states a received quantity and, only when a remainder exists, a `CardTransferProduct.DifferenceAction` (`ReturnedToSource` or `KeptAtDestination`, per-line): `ReturnedToSource` mirrors the Known-way "returned" path exactly — the target's `Hold` rises by the remainder and an auto-generated return transfer carries it back, itself requiring its own `receive` call; `KeptAtDestination` instead credits the target's `Available` with the *full* transacted quantity and spawns nothing — the gap between what was confirmed and what was credited stays visible via `DifferenceAction` rather than being expressed as stock in transit. Disposal is not supported for an Unknown-way line (`TransferErrors.DisposalNotAllowedForUnknown`) — there is no physical card to write off, so `POST /{id}/dispose` rejects outright if any open line on the transfer is Unknown-way.
- **Fulfilment credit gating:** Only a transfer whose `TargetBranchId` equals the branch-request's `RequestingBranchId` credits `BranchRequestItem.ReceivedQuantity` — a return transfer heads *away* from the requesting branch, so even though it inherits `BranchRequestId` (decision D-04), it never credits (checked explicitly in `BranchRequestFulfilment.ApplyReceiptAsync`). Post the Unknown-way Maker-Checker workflow, `BranchRequestFulfilment` is the credit path for **every** settled line, Known or Unknown — a branch-request confirm only stages transfers now, it never settles one, so the "credited inline at confirm" case no longer exists for any line shape.
- **Constraint (DB):** `SourceBranchId <> TargetBranchId` (check constraint `CK_CardsTransferHistory_SourceNotTarget`), also pre-empted in the service layer before it would ever hit the DB.
- **Extraction:** `ITransferComposer` (`ValidateAsync` + `StageAsync`) is the shared core behind both direct create (`TransactionsController`) and branch-request confirm (`BranchRequestsController` → `BranchRequestService.ConfirmAsync`). `StageAsync` also takes the acting account's username (Maker identity) as an explicit parameter — actor resolution stays with each caller, matching how `tenantId` is already handled. Not covered by the composer: the 500-card-per-transfer cap (direct-create only, `TransferService.ValidateCreateShape`), actor resolution itself, audit staging, detail reload/mapping — these stay with each caller.

### Card Disposal
- **Responsibility:** Permanently write off cards, with a mandatory reason and the responsible branch.
- **Main entities:** `CardDisposal` (header; append-only, no soft delete — a mistaken disposal is corrected by a compensating inbound batch, never undone), `CardDisposalItem` (one row per card written off; rows exist for both Known- and Unknown-way disposals).
- **Why its own aggregate rather than columns on `ProductItem`:** one operational event ("water-damaged box") covers many cards; storing the reason per card would repeat it N times with no way to query "what did branch X write off last quarter and why."
- **`CardDisposal.CardTransferId`** is set when the disposal is settling an in-flight transfer, `null` when cards sitting at a branch are disposed outside any transfer.

### Branch Requests (API §4.9)
- **Responsibility:** A branch records a need for stock; another branch's transfer(s), created via `confirm`, fulfil it — fully or partially, possibly across multiple transfers.
- **Main entities:** `BranchRequest` (append-only-with-status — **not** `AuditableEntity`, no soft delete (decision Q-09), no query filter, no restore endpoint — matches the `CardTransfer` precedent), `BranchRequestItem` (one line per requested product; two independent, never-decremented counters: `DispatchedQuantity`, `ReceivedQuantity`).
- **Status is computed, not assigned ad hoc (decision D-03):** every transition except the two terminal closures (`Refused`, `Cancelled`) flows through `BranchRequest.RecomputeStatus()`, a pure function of the line counters, evaluated strongest-condition-first (`Fulfilled` > `PartiallyFulfilled` > `Confirmed` > `PartiallyConfirmed` > `InProgress`). An empty `Items` collection resolves to `InProgress`, never vacuously to `Fulfilled`.
- **Two independent counters, not one (decision D-01):** `DispatchedQuantity` — cumulative across every transfer generated for the line, credited in `ConfirmAsync` regardless of Known/Unknown way. `ReceivedQuantity` — cumulative actually credited to the requesting branch, credited by `BranchRequestFulfilment.ApplyReceiptAsync` when the generated transfer settles (`TransferService.SettleAsync`). This is now uniform across Known- and Unknown-way lines: post the Unknown-way Maker-Checker workflow, a confirm only *stages* transfers, it never settles one — the earlier "Unknown-way credits inline at confirm, before any receive call" behavior no longer exists for either way.
- **Over-fulfilment is allowed** (decision Q-03) — neither counter is ever decremented, and neither can be derived from the other.
- **Unrequested products:** a confirming transfer plan may include product lines the original request never asked for; these credit nothing on the request (decision D-05) — they're surfaced only via the derived `UnrequestedProducts` collection on `GET /api/inventory/requests/{id}`, computed at read time from every transfer linked by `BranchRequestId`.
- **Creation-time blocks:** duplicate open request for the same branch+product is rejected (`GetOpenProductIdsForBranchAsync`); requesting for an inactive branch is rejected outright (a request against an inactive branch could never be confirmed, since the branch would always fail as a transfer target); no per-request volume cap (decision Q-06, unlike `TransferService`'s 500-card cap on direct create).
- **Confirm-time block:** `plan.SourceBranchId == branchRequest.RequestingBranchId` is rejected before even calling `ITransferComposer.ValidateAsync` (pre-empts the DB's source≠target check with a request-specific error).
- **Closure (decision D-06):** `Refuse`/`Cancel` allowed only from `InProgress` or `PartiallyConfirmed` — once anything has been received, the request cannot be walked back (already-dispatched transfers still complete their own §4.10 lifecycle independently).
- **Authorization:** system admins have read-only access across tenants; every write (create/confirm/refuse/cancel) rejects an admin token outright (`SystemAdminNotAllowed`) — same reasoning as `CardTransfer.CreatedByTenantId`: there's no admin-tenant id to attribute the action to. Any authenticated tenant user may settle a transfer without a branch-level check (no per-user identity below the tenant level exists to check against).
- **Concurrency:** `RowVersion` on `BranchRequest`, added beyond the original ERD (decision Q-07) specifically so two concurrent confirm calls can't both read a non-terminal status and both generate transfers.

---

## 5. Database

**Technology:** SQL Server, accessed via EF Core 8.0.27 (`Microsoft.EntityFrameworkCore.SqlServer`). One `DbContext`: `InfrastructureLayer.Data.AppDbContext`.

**Migration strategy:** Migrations are committed as source code in `InfrastructureLayer/Migrations/`. `DbSeeder.MigrateAndSeedAsync` calls `context.Database.MigrateAsync()` on every app startup (idempotent) and seeds the bootstrap `SystemAdmin` from the `SeedAdmin:Username`/`SeedAdmin:Password` config section if no admin row exists yet (logs a warning and skips, rather than failing startup, if that section isn't configured). As of this pass, latest migration: `20260810120000_U1-UnknownTransferMakerChecker`.

Model-changing commands (per README, confirmed still accurate):
```bash
dotnet ef migrations add <name> --project InfrastructureLayer --startup-project InventoryManagmentAndInstanceIssuancePresentationLayer
dotnet ef database update --project InfrastructureLayer --startup-project InventoryManagmentAndInstanceIssuancePresentationLayer
```

**Soft delete:** `AuditableEntity` base (`CreatedAt`, `UpdatedAt`, `DeletedAt`, `IsDeleted`, `DeletedBy`) — used by `Tenant`, `SystemAdmin`, `Branch`, `Product`, `Batch`, `ProductItem`, `Stock`. A global query filter `!IsDeleted` is applied per-entity in `OnModelCreating` (not automatic for new `AuditableEntity` subtypes — must be added explicitly per entity, e.g. `entity.HasQueryFilter(b => !b.IsDeleted)`). `CardTransfer`, `CardTransferProduct`, `CardTransferItem`, `CardDisposal`, `CardDisposalItem`, `BranchRequest`, `BranchRequestItem` are all deliberately append-only and do **not** derive from `AuditableEntity` — no soft delete, no query filter, no restore endpoint for any of them.

**Multi-tenancy:** No global EF query filter for tenant isolation (only soft-delete has one). Tenant scoping is manual — enforced by explicit tenant-id parameters on repository methods and by services resolving the caller's tenant via `ICurrentTenant` before querying. This means a new repository method must remember to filter by tenant itself; nothing in the DbContext does it automatically.

**Concurrency (`RowVersion`):** `[Timestamp] byte[] RowVersion` on `Stock`, `CardTransfer`, `BranchRequest` — guards status/quantity flips against lost updates. `DbUpdateConcurrencyException` is caught explicitly in the relevant services (mapped to a 409 module error) and, as a fallback, in `GlobalExceptionMiddleware`.

**Important tables and relationships (see `AppDbContext.OnModelCreating` region methods for full Fluent API):**

| Table | Key | Notable indexes/constraints |
|---|---|---|
| `Tenants` | `Id` | Unique `Code`, unique `Username` (both across all rows including soft-deleted — identifiers stay reserved) |
| `SystemAdmins` | `Id` | Unique `Username` filtered `IsDeleted=0` |
| `RefreshTokens` | `Id` | Unique `TokenHash`; index on `userName` |
| `Branches` | `Id` | Unique `(TenantId, Name)` filtered `IsDeleted=0` |
| `Products` | `Id` | Unique `(TenantId, Name)` filtered `IsDeleted=0` |
| `Stocks` | Composite `(TenantId, BranchId, ProductId)` | `AvailableQuantity >= 0`, `HoldQuantity >= 0` check constraints |
| `Cards` (`ProductItem`) | `ID` | Unique `(TenantId, PanFingerprint)` filtered `IsDeleted=0` (identity/dedup); `PanFingerprint` is `binary(32)` fixed-length; covering index `(TenantId, ProductId, BranchID, PanFingerprint)`; index `(TenantId, BranchID, Status)` for the unassigned-pool/availability queries |
| `Batches` | `Id` | Unique `(UploadedByTenantId, FileMac)` filtered `IsDeleted=0` (duplicate-file guard) |
| `CardsTransferHistory` (`CardTransfer`) | `Id` | Check `SourceBranchId <> TargetBranchId`; indexes on `(TenantId, CreatedAt)`, `(TenantId, BranchRequestId)`, `(TenantId, TransactionStatus)`, `(TenantId, Origin)`, `ParentTransferId`, `SourceBranchId`, `TargetBranchId` |
| `CardTransferProducts` | `Id` | Unique `(CardTransferId, ProductId)`; checks: `TransactedQuantity > 0`, `RealQuantityReceived >= 0` (nullable), `DisposedQuantity >= 0` (nullable), and the settlement-identity check `RealQuantityReceived + DisposedQuantity <= TransactedQuantity` |
| `CardTransferItems` | `Id` | Unique `(CardTransferId, ProductItemId)`; `ProductItemId` FK is `NoAction` (also blocks a batch delete cascading through an in-flight card) |
| `CardDisposals` | `Id` | Indexes on `(TenantId, DisposedAt)`, `(TenantId, BranchId)`, `CardTransferId`; `Reason` required, max 500 |
| `CardDisposalItems` | `Id` | Unique `(CardDisposalId, ProductItemId)` |
| `BranchRequests` | `Id` | Indexes on `(TenantId, RequestStatus)`, `(TenantId, RequestingBranchId)`, `(TenantId, RequestDateTime)` |
| `BranchRequestItems` | `Id` | Unique `(RequestId, ProductId)`; checks: `AskedQuantity > 0`, `DispatchedQuantity >= 0`, `ReceivedQuantity >= 0` |
| `AuditLogs` | `Id` (long) | Indexes on `(TenantId, Timestamp)`, `(EntityName, EntityId)`, `(ActorTenantId, Timestamp)`; no soft delete, immutable |

**Two-navigation-to-same-target pitfall (repeated pattern, worth knowing before adding a new entity):** Several entities have two separate FKs to `Tenants` (e.g. `CardTransfer.Tenant` / `CreatedByTenant`; `BranchRequest.Tenant` / `ActionTakenByTenant`; `CardDisposal.Tenant` / `DisposedByTenant`). Each needs its **own** navigation property — a navigation-less `HasOne<Tenant>().WithMany()` call for both would make EF Core silently reconfigure the same relationship and leave the second FK unmapped. This is called out in multiple entity/DbContext doc comments; preserve the pattern for any future dual-tenant-FK entity.

**Schema additions beyond the original ERD** (all explicitly flagged "Flagged for DBA review" in code comments — worth a real DBA pass before this platform reaches production scale): `RefreshToken` (whole table), `CardTransfer.ActionNotes` + `RowVersion`, `BranchRequest.RowVersion`, `BranchRequestItem.DispatchedQuantity` + `ReceivedQuantity`, `CardTransferProduct.DisposedQuantity`, `TransactionOrigin` enum, `CardStatus.Disposed`, `TransactionStatus.PartiallyReceived` + `.Disposed`, `TransactionItemReceiveStatus.Disposed`, `BranchRequestStatus.PartiallyConfirmed`/`.PartiallyFulfilled`/`.Fulfilled`.

---

## 6. APIs

All controllers require `[Authorize]` (JWT bearer) except `AuthController`'s login endpoints. Every response uses the `ApiResponse<T>` envelope. List endpoints return `PaginatedResponse<T>` inside `Data`.

| Controller | Route | Endpoints |
|---|---|---|
| `AuthController` | `api/auth` | `POST tenant`, `POST admin`, `POST refresh`, `POST logout`, `GET me` |
| `TenantsController` | `api/tenants` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `PUT {id}/password`, `DELETE {id}`, `POST {id}/restore` — system-admin only (`SystemAdminOnly` policy, see §7) |
| `BranchController` | `api/branches` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`, `POST {id}/restore`, `POST {id}/activate`, `POST {id}/deactivate` |
| `ProductController` | `api/products` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`, `POST {id}/restore`, `POST {id}/activate`, `POST {id}/deactivate` |
| `StockController` | `api/stock` | `GET`, `GET branches/{branchId}` |
| `ProductItemsController` | `api/product-items` | `GET`, `GET {id}`, `PUT {id}` |
| `InventoryController` | `api/inventory` | `POST upload` (batch upload) |
| `InventoryHistoryController` | `api/inventory/history` | `GET` |
| `CardFilesController` | `api/card-files` | `POST` (generate) |
| `TransactionsController` | `api/inventory/transactions` | `GET`, `GET {id}`, `POST` (create transfer), `POST {id}/receive`, `POST {id}/dispose` |
| `CardDisposalsController` | `api/inventory` | `POST cards/dispose`, `GET disposals`, `GET disposals/{id}` |
| `BranchRequestsController` | `api/inventory/requests` | `GET`, `GET {id}`, `POST`, `POST {id}/confirm`, `POST {id}/refuse`, `POST {id}/cancel` |

**Request/response conventions:**
- Success: `{ success: true, data: <T>, error: null }`.
- Failure: `{ success: false, data: null, error: { code, message, category, ... } }` — `code` is stable and machine-readable (module-prefixed, e.g. `"BranchRequest.NotOpenForConfirmation"`); `message` is localized server-side per `Accept-Language`.
- Validation failures from model binding (missing/malformed fields) are normalized to the same envelope, HTTP 422, via `ValidationResponseFactory` — not ASP.NET Core's default 400.
- List endpoints accept a filter DTO via `[FromQuery]` and return `PaginatedResponse<T>` (`data`, `pageNumber`, `pageSize`, `totalCount`, `totalPages`).
- DTO naming: the Branch Requests module deliberately prefixes its DTOs `StockRequest*` (`CreateStockRequest`, `StockRequestDetailResponse`, etc.) to avoid colliding with the `CreateBranchRequest`-style naming that would otherwise be expected — worth remembering when searching for these types.
- Concurrency tokens are surfaced to clients as base64 strings (e.g. `StockRequestDetailResponse.RowVersion` via `Convert.ToBase64String(...)`), not raw byte arrays.
- Correlation: every response carries `X-Trace-Id` (from `GlobalExceptionMiddleware`, tied to `HttpContext.TraceIdentifier`), useful for correlating a client bug report with server logs.

**Versioning:** None — no API version segment in any route. ⚠️ Unclear/requires confirmation whether versioning is planned.

---

## 7. Authentication & Authorization

**Mechanism:** JWT bearer tokens (HMAC-SHA256, `Microsoft.AspNetCore.Authentication.JwtBearer`), issued by `JwtTokenGenerator` (`InfrastructureLayer.Security`).

**JWT claims:**
- `username` — the authenticated principal's username (tenant or system admin).
- `isSystemAdmin` — `"true"`/`"false"` string; drives the `SystemAdminOnly` policy and `ICurrentTenant.IsSystemAdmin`.
- `tenantId` — present only on tenant tokens (via the `CreateForTenant(string username, long tenantId)` overload); absent on system-admin tokens.
- `jti` (`JwtRegisteredClaimNames.Jti`) — a fresh GUID per token.

**Access/refresh lifetimes:** `JwtOptions.AccessTokenMinutes` (default 480 = 8h), `RefreshTokenDays` (default 7). Signing key supplied via `Jwt:SigningKey` — **must** come from user-secrets/environment; `Program.cs` throws at startup if it's empty (`EnsureJwtSigningKeyPresent`).

**Refresh tokens:** Persisted (`RefreshToken` entity), only a **hash** of the raw token stored (`TokenHash`); rotation on `/api/auth/refresh` revokes the current token and links the successor via `ReplacedByTokenHash` (reuse-detection scaffold). `IsSystemAdmin` and `TenantId` are captured at issue time so rotation reissues the same kind of token without a fresh DB lookup for that decision.

**Authorization model:** `[Authorize]` only — **no permission/role attribute system**. The single named policy is `SystemAdminOnly` (`AuthorizationPolicies.SystemAdminOnly`), requiring the `isSystemAdmin` claim to equal `"true"`/`"True"`; used to gate `TenantsController` to system admins. Everywhere else, authorization is enforced in the service layer by branching on `ICurrentTenant.IsSystemAdmin` / `.TenantId` — e.g. every write path in `BranchRequestService` and `TransferService` rejects a system-admin token outright with a module-specific error, because those aggregates' "acting tenant" FK has nowhere to point for an admin.

**Tenant isolation:** Manual, not automatic (see §5) — `ICurrentTenant` (implemented by `InventoryManagmentAndInstanceIssuancePresentationLayer.Security.CurrentTenant`, reading JWT claims off the current `HttpContext`) is injected wherever a service or the `AppDbContext`/interceptor needs to know who's asking; there's no query filter doing this for you.

**Known dead code (flagged during this pass, not yet cleaned up):** `JwtTokenGenerator` has a single-argument `CreateForTenant(string username)` overload and a `TenantId` property that unconditionally throws `NotImplementedException`. Neither is referenced anywhere in the codebase (`IJwtTokenGenerator` only declares the two-argument `CreateForTenant(string, long)`, which is what `AuthService` actually calls). Safe to delete; harmless as-is since nothing calls it, but a landmine if something ever does.

**Important security assumptions:**
- Full PANs are never persisted — only `MaskedPan` (display) and `PanFingerprint` (HMAC-SHA256, keyed per-tenant via PBKDF2, `binary(32)`, identity/dedup only). See §11.
- Passwords (`Tenant.PasswordHash`, `SystemAdmin.PasswordHash`) are PBKDF2-hashed via `Pbkdf2PasswordHasher`.
- Three independent secrets, deliberately never shared (key separation): `Jwt:SigningKey`, `BatchCipher:MasterSecret`/`:Salt`, `PanHash:MasterSecret`/`:Salt`. All three fail app startup if missing (`ValidateOnStart()` / explicit checks in `Program.cs`).

---

## 8. Background Jobs

None exist in this codebase. Batch upload — the one workload that might elsewhere be a background job — runs synchronously inside the HTTP request/DB transaction (see §4, Batch Upload). No Hangfire or equivalent is referenced anywhere in the `.csproj` files.

---

## 9. Integrations

None. No Firebase/FCM, Azure services, blob storage, payment provider, email/SMS, Redis, or APM/telemetry integration exists in this codebase as of this pass. The "integration surface" is entirely file-based: encrypted `.dat` files in (batch upload) and out (card-file generation), both AES-based and both implemented locally (`BatchFileCipher`, `CardFileWriter`) with no external service call.

---

## 10. Configuration

Configuration sources: `appsettings.json` (defaults, no secrets), `appsettings.Development.json`, user-secrets (`UserSecretsId` set in the Presentation `.csproj`) for local dev, environment variables for other environments. Bound via the Options pattern (`IOptions<T>`), one class per concern in `ApplicationLayer/Options`.

| Section | Class | Required keys | Notes |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | — | SQL Server connection string | Committed default targets `.\SQLEXPRESS`; override per environment. |
| `Jwt` | `JwtOptions` | `SigningKey` (secret) | `Issuer`, `Audience`, `AccessTokenMinutes` (480), `RefreshTokenDays` (7) have defaults/committed values. |
| `CardFile` | `CardFileOptions` | none (all defaulted) | `MaxCardsPerRequest` = 50,000. |
| `BatchCipher` | `BatchCipherOptions` | `MasterSecret`, `Salt` (both secrets) | Validated on start. |
| `PanHash` | `PanHashOptions` | `MasterSecret`, `Salt` (both secrets, `Salt` must be valid base64) | Validated on start; deliberately separate secret from `BatchCipher`. |
| `LogEncryption` | `LogEncryptionOptions` | `Password`, `Salt` (both secrets) | App throws before host build if missing; `Directory`/`ErrorFileName`/`ExceptionFileName` defaulted. |
| `SeedAdmin` | (raw config, no strongly-typed class) | `Username`, `Password` | If absent, seeding is skipped with a warning log rather than failing startup — so a fresh environment without this configured has **no system admin** until it's set and the app restarts. |

**Never write actual secrets, passwords, tokens, connection strings, API keys, or credentials into this file.** Use `<SECRET>` as a placeholder if an example is ever needed.

---

## 11. Important Invariants & Business Rules

These must not be broken by future changes without a deliberate, reviewed decision:

- Every tenant-owned row is scoped to exactly one tenant; tenant scoping is enforced **manually** in repository/service code, not by a global query filter — a new repo method must explicitly filter by tenant.
- Full PAN is **never** persisted anywhere, logged, or returned by any API. Only `MaskedPan` (display) and `PanFingerprint` (identity/dedup) exist in the database.
- `Product.ProductTransactionWay` is immutable once any `ProductItem` exists for that product.
- A `CardTransfer`'s `SourceBranchId` must never equal its `TargetBranchId` (DB-enforced).
- A `CardTransferProduct`'s `RealQuantityReceived + DisposedQuantity` must never exceed `TransactedQuantity` (DB-enforced).
- An Unknown-way `CardTransferProduct` line's `DisposedQuantity` is always `0` — disposal is not supported for Unknown-way (app-enforced, `TransferErrors.DisposalNotAllowedForUnknown`; there is no DB constraint tying this to `ProductTransactionWay`, so a future change must not assume the DB catches a regression here).
- `CardTransfer.CreatedByUsername` is always recorded (Maker identity); `.CheckedByUsername` is set exactly once, at settlement (Checker identity). It is by design acceptable for both to be the same account.
- `Stock.AvailableQuantity` and `Stock.HoldQuantity` must never go negative (DB-enforced).
- A `ProductItem` with `BranchID = null` is untouchable by anything outside the Transactions module (no re-sight, no status update, no soft delete).
- `BranchRequestItem.DispatchedQuantity` and `.ReceivedQuantity` are cumulative and **never decremented** — over-fulfilment is allowed by design (decision Q-03).
- A return transfer (`TransactionOrigin.AutoGeneratedReturn`) inherits its parent's `BranchRequestId` but must never credit `BranchRequestItem.ReceivedQuantity` — fulfilment credit is gated strictly on `TargetBranchId == RequestingBranchId`.
- `BranchRequest.RequestStatus` is only ever assigned directly for the two terminal closures (`Refused`, `Cancelled`); every other transition must go through `RecomputeStatus()`.
- `BranchRequest`/`CardTransfer`/`CardDisposal`/their line-item types are append-only: no soft delete, no restore endpoint, no `AuditableEntity`. Do not add soft-delete to these without revisiting every place that currently assumes they're permanent (audit trail integrity, `RecomputeStatus`, disposal irreversibility).
- A system admin can never create or settle a transfer, or create/confirm/refuse/cancel a branch request — every such write path must keep rejecting an admin token outright, because these aggregates' "acting tenant" columns are non-nullable FKs with nowhere for an admin id to go.
- Card disposal is deliberately irreversible; a mistaken disposal is corrected by a compensating inbound batch, never by "undoing" the disposal record.
- Three cryptographic secrets (JWT signing key, batch-cipher master secret, PAN-hash master secret) must remain independent — never derive one from another, never let a rotation of one implicitly affect another.

---

## 12. Important Technical Decisions

### Decision: Result pattern instead of exceptions for business outcomes
**Status:** Active
**Decision:** All service-layer business outcomes return `Result`/`Result<T>`; exceptions are reserved for truly unexpected faults, caught only by `GlobalExceptionMiddleware`.
**Reason:** Deterministic, type-safe error handling; `ErrorCategory` gives a single source of truth for HTTP status mapping without inspecting message text.
**Impact:** Any new service method must return `Result`/`Result<T>`, not throw, for expected failure modes. `DbUpdateConcurrencyException`/`DbUpdateException` from EF Core are the one place exceptions are still explicitly caught inside services (concurrency/persistence conflicts), because EF Core itself throws for those rather than returning a value.

### Decision: Manual tenant scoping, not global EF query filters
**Status:** Active
**Decision:** Only soft-delete uses a global query filter; tenant isolation is enforced by explicit tenant-id parameters through repositories and services.
**Reason:** ⚠️ Unclear/requires confirmation — not stated explicitly in code comments found during this pass. Plausible reasons include needing system-admin cross-tenant reads (which a blanket filter would complicate) and wanting explicit, auditable tenant checks at each call site rather than implicit ones.
**Impact:** Every new repository method touching a tenant-owned table must take and apply a tenant scope parameter itself. There is no safety net if one is forgotten — this is the single highest-risk pattern in the codebase from a security standpoint and deserves care in review.

### Decision: `ProductTransactionWay` split (Known vs Unknown) drives most of the Transfers/Branch-Requests complexity
**Status:** Active
**Decision:** A product's items are either individually tracked (`Known`) or only tracked by quantity (`Unknown`), snapshotted onto each transfer line at creation time.
**Reason:** Some card types (per the business domain) are never individually serialized/tracked by the tenant; forcing item-level tracking on them would be both false precision and an operational burden.
**Alternatives considered:** ⚠️ Unclear/requires confirmation.
**Impact:** Almost every settlement code path (`TransferService.receive`, `BranchRequestFulfilment`, `ITransferComposer.StageAsync`) branches on this value. Any new feature touching transfers or branch requests must handle both paths — Unknown-way is not a degenerate case of Known-way, it has genuinely different data flow (no `CardTransferItem` rows ever, no disposal support). Since the Unknown-way Maker-Checker workflow, both ways *do* share the same create → pending → settle shape and the same settlement entry point (`POST /{id}/receive`); the remaining difference is that Unknown-way resolves its remainder via a per-line `DifferenceAction` instead of per-card dispositions, and never supports disposal.

### Decision: Unknown-way Maker-Checker workflow (supersedes the "Unknown Inventory Refactor")
**Status:** Active
**Decision:** An Unknown-way transfer line no longer settles immediately at creation/confirm. It follows the same create (`Available → Hold` at the source only) → pending (`InProgress`) → settle (`receive`) lifecycle a Known-way line already had. At settlement, a remainder is resolved per line via `CardTransferProduct.DifferenceAction`: `ReturnedToSource` spawns an auto-generated return transfer exactly like the Known-way "returned" path; `KeptAtDestination` credits the target's `Available` with the full transacted quantity and spawns nothing, leaving the received-vs-credited gap visible via `DifferenceAction` rather than expressed as stock in transit. Disposal is not supported for Unknown-way — there is no physical card to write off.
**Reason:** The prior "settle immediately" behavior gave Unknown-way transfers no maker/checker separation and no window to correct a quantity discrepancy before it became final stock movement — this workflow was requested specifically to close that gap.
**Impact:** `CardTransfer.CreatedByUsername`/`.CheckedByUsername` (new, not in the original ERD) record the acting account's identity on every transfer, Known- or Unknown-way — it is explicitly acceptable for the same account to be both, since this system has one account per tenant and there is no per-user identity to compare; the requirement is that identity be recorded, not that two distinct identities be proven. `BranchRequestFulfilment.ApplyReceiptAsync` is now the credit path for every settled line regardless of way (§4, Branch Requests). `ReceiveTransferLine.DifferenceAction` and `TransferProductResponse.DifferenceAction` are new DTO fields; four new `TransferErrors` cover the Unknown-way settlement validation (`DifferenceActionRequired`, `DifferenceActionNotApplicable`, `InvalidDifferenceAction`, `DisposalNotAllowedForUnknown`).

### Decision: `ITransferComposer` extraction (Validate/Stage split)
**Status:** Active
**Decision:** `TransferService.CreateAsync`'s original monolithic logic was split into `ValidateAsync` (read-only) and `StageAsync` (writes, must run inside an ambient transaction), exposed as `ITransferComposer`, so `BranchRequestService.ConfirmAsync` can validate every plan across multiple transfers before staging any of them.
**Reason:** A branch-request confirm can generate N transfers from N source branches in one call; validating all N up front (fail before any write) requires the validate/write split that a single monolithic `CreateAsync` didn't have.
**Alternatives considered:** ⚠️ Unclear/requires confirmation.
**Impact:** Any future change to transfer creation rules (branch/product validation, Known/Unknown item-id shape rules, card selection) must go in `TransferComposer`, not duplicated in `TransferService` or `BranchRequestService` — both callers depend on it staying the single source of truth.

### Decision: Two independent branch-request counters (`DispatchedQuantity`, `ReceivedQuantity`)
**Status:** Active
**Decision:** A `BranchRequestItem` tracks how much was sent and how much actually arrived as two separate, never-decremented counters rather than one.
**Reason:** Over-fulfilment is allowed (Q-03), so neither value is derivable from the other; "how much was sent" and "how much arrived" answer genuinely different questions once returns/disposals are possible mid-flight.
**Impact:** `RecomputeStatus()` reads both; any new fulfilment path must credit the correct one at the correct time (dispatched at confirm/stage time, received at settlement time — uniformly for Known- and Unknown-way since the Unknown-way Maker-Checker workflow).

---

## 13. Known Constraints

- **Target framework mismatch:** the API Endpoint Specification document references .NET 10; the actual codebase targets `net8.0` throughout and has not been retargeted (confirmed in README and every `.csproj`). Do not assume .NET 10-only features are available.
- **No test project exists in the repository** as of this pass (confirmed by filesystem search) — Phase 8 (batch-upload test suite) and the card-file-generation round-trip integration test (§9.8) remain open per prior planning notes; there is currently no automated regression safety net for any module.
- **No async/queued processing** — batch upload's synchronous-in-request design means very large files will hold a DB transaction open for the whole pipeline; there is no chunking or background-job path today.
- **SQL Server only** — no abstraction layer for a different provider; Fluent API uses SQL Server-specific constructs (`binary(32)`, `GETUTCDATE()`, filtered indexes with `[Col] = 0` syntax).
- **Single system admin bootstrap, no admin management UI/API beyond seeding** — `SystemAdmin` rows exist as a table (rotatable/auditable per its own doc comment) but no controller currently manages them beyond the one seeded at startup; ⚠️ Unclear/requires confirmation whether additional system-admin management endpoints are planned.

---

## 14. Development Workflow

**Build:**
```bash
dotnet restore
dotnet build
```

**Run:**
```bash
dotnet run --project InventoryManagmentAndInstanceIssuancePresentationLayer
```

**Migrations:**
```bash
dotnet ef migrations add <name> --project InfrastructureLayer --startup-project InventoryManagmentAndInstanceIssuancePresentationLayer
dotnet ef database update --project InfrastructureLayer --startup-project InventoryManagmentAndInstanceIssuancePresentationLayer
```

**Local development requirements:** .NET 8 SDK, SQL Server (LocalDB/Express/full), and the following via `dotnet user-secrets` (Presentation project's `UserSecretsId` is already set) before the app will start:
- `Jwt:SigningKey`
- `BatchCipher:MasterSecret`, `BatchCipher:Salt`
- `PanHash:MasterSecret`, `PanHash:Salt` (Salt must be valid base64)
- `LogEncryption:Password`, `LogEncryption:Salt`
- `SeedAdmin:Username`, `SeedAdmin:Password` (optional — skipped with a warning if absent, but then no admin exists)

**Session/delivery convention used on this project (not a tooling requirement, but the established working pattern with the AI pairing on this repo):** one phase per session, delivered as a `git format-patch`-style file, applied and pushed by the repo owner between sessions. See the root-level `*.patch` files for the full delivery history.

**Test commands:** ⚠️ No test project exists — nothing to run yet (see §13).

---

## 15. Deployment / Infrastructure

⚠️ Unclear / requires confirmation — no CI/CD configuration (GitHub Actions, Azure Pipelines, etc.), Dockerfile, or deployment manifest was found in the repository during this pass. No environment-specific `appsettings.*.json` beyond `Development` was found. This section should be filled in once a real deployment pipeline exists; do not invent one.

---

## 16. Testing Strategy

No test project exists in the repository (confirmed by filesystem search during this pass — see §13). Verification of new code during development has instead relied on:
- Roslyn semantic compilation against the real layer DLLs (no `dotnet` SDK available in some working sandboxes).
- Python round-trip simulation for cipher/parser logic when a full .NET toolchain isn't available.
- OpenAPI YAML validation (`openapi-spec-validator`) where relevant.

**Areas most in need of real automated regression coverage**, given their complexity and the number of interacting invariants (§11):
- `TransferService` settlement (`receive`/`dispose`), especially the Known/Unknown disposition validation and the auto-generated-return chain.
- `BranchRequestService.ConfirmAsync` + `ITransferComposer` interaction (multi-transfer confirm, partial validation-then-stage).
- `BranchRequestFulfilment.ApplyReceiptAsync` credit gating (D-04/D-05).
- Batch upload end-to-end (decrypt → parse → validate → persist → failed-rows report), including the "no Failed status on transient exception" behavior.
- PAN fingerprint/masking round-trip and per-tenant key derivation.

---

## 17. Known Risks / Technical Debt

- **Manual tenant scoping (see §11, §12):** no compiler or runtime safety net catches a repository method that forgets to filter by tenant. Impact: potential cross-tenant data leakage from a single missed parameter. Current workaround: code review discipline + the established "explicit tenant scope parameter" convention. Possible future direction: a global query filter keyed off `ICurrentTenant`, with an explicit bypass mechanism for system-admin reads (mirroring how soft-delete's filter already works).
- **Dead code in `JwtTokenGenerator`:** unused `CreateForTenant(string)` overload and a `TenantId` property that throws `NotImplementedException`. Impact: low (nothing calls it) but it's a landmine for a future caller who reaches for the wrong overload. Workaround: none needed today. Future direction: delete both members.
- **No automated test suite:** see §13/§16. Impact: regressions in the (numerous, intricate) settlement/fulfilment invariants are only caught by manual review or production incidents. Future direction: stand up the Phase 8 batch-upload test suite and the §9.8 card-file round-trip test that were already planned but never delivered, then expand to Transfers/Branch-Requests.
- **Schema additions beyond the original ERD, several "Flagged for DBA review" in code comments** (full list in §5): functionally shipped and working, but not yet formally reviewed against the ERD by a DBA as the comments themselves note. Impact: low functional risk, but a formal reconciliation pass against the ERD document would reduce drift risk for future schema work.
- **Known-way transfer settlement requires explicit per-card dispositions from the caller** (see §4, Transfers): currently the only supported shape; per prior planning notes this was flagged as an open design question rather than a settled decision. Confirm with the repo owner before changing — this is core settlement logic touched by both direct transfers and branch-request fulfilment.
- **`SeedAdmin` silently skips seeding when unconfigured**, logging only a warning. Impact: a fresh environment can come up with zero system admins and no obvious startup failure pointing at why admin login doesn't work. Future direction: consider making this configurable-but-loud (e.g. a startup log at `Error` level, or a documented health-check).

---

## 18. Change History

Keep this section SHORT — only major architectural or behavioral changes.

- **2026-08-10:** Initial creation of this document via full repository read (entities, DbContext, contracts, repositories, services, controllers, DI wiring, security, options/config, migrations, localization). Confirmed README's "not yet implemented" closing paragraph is stale — entities, DbContext, repositories, services, and controllers are all implemented; flagged for the repo owner to update or remove. Branch Requests module (API §4.9) confirmed fully implemented end-to-end (R1–R6 committed), superseding an earlier "service/controller pending" status.
- **2026-08-10 (same day, follow-up pass):** Unknown-way Maker-Checker workflow implemented (migration `U1-UnknownTransferMakerChecker`). Unknown-way transfer lines no longer settle immediately at creation/confirm — they now follow the same create → pending → settle lifecycle Known-way lines already had, with remainder resolution via a new per-line `DifferenceAction` (`ReturnedToSource`/`KeptAtDestination`) instead of per-card dispositions, and no disposal support. `CardTransfer.CreatedByUsername`/`.CheckedByUsername` record Maker/Checker identity on every transfer — same account for both is explicitly acceptable, since this system has one account per tenant (§7) and there is no per-user identity to compare. This also changes Branch Requests (§4.9) behavior: an Unknown-way line confirmed via a branch request no longer credits `ReceivedQuantity` inline at confirm time — `BranchRequestFulfilment.ApplyReceiptAsync` is now the credit path for every settled line, Known or Unknown. Corrected two pre-existing stale claims found during this pass: `CardTransferItem`'s doc comment and this document's own §4 previously (incorrectly) stated Unknown-way lines get item rows — they never have, in code. **Verification note:** this pass had no .NET SDK or NuGet access in the working sandbox, so changes were verified by careful manual re-read rather than the usual Roslyn semantic compile — a real build pass is recommended before merging.
- **2026-08-12 (Swagger documentation, Phase S1 of a 5-phase plan):** Wired `IncludeXmlComments` for the first time across all three XML-doc-producing assemblies (previously only the Presentation project had `GenerateDocumentationFile` enabled, and `IncludeXmlComments` was never called at all — no existing XML doc comment, however complete, ever reached Swagger UI). Added `EnumSchemaDescriptionsFilter` and `ExamplesOperationFilter`/`ExampleCatalog` (see §3, "Swagger / API documentation" — no new NuGet package, extends the existing hand-written-filter pattern). Full example content delivered for `AuthController` and `TenantsController` only; S2-S5 cover the remaining 13 controllers. Found and corrected a real inaccuracy while building this: several `[ProducesResponseType]` declarations for 401/403 claimed a typed `ApiResponse<T>` envelope body for responses that the ASP.NET Core authorization middleware actually returns empty — fixed on Auth/Tenants, flagged as an open per-controller audit item for S2-S5 (§3). Also fixed a stale `<param name="TenantId">` doc on `CurrentPrincipalResponse` (ApplicationLayer/DTOs/Auth/AuthDtos.cs) that documented a parameter the record doesn't have. **Verification note:** same sandbox constraint as the entry above — no .NET SDK/NuGet access, verified by manual cross-check of every new file (brace balance, using statements, cref resolution, exact codes/messages against the real `*Service`/`*Errors` source) rather than a real build; a real `dotnet build` is recommended before merging.
- **2026-08-12 (same day, Swagger documentation Phase S2):** Full example content delivered for the Catalog & Printing modules: `BranchController`, `ProductController`, `ProductPrintConfigController`, `PrintersController`, `PrintImagesController` (29 endpoints). Continued the per-controller 401/403 accuracy audit from S1 (§3) — found the opposite issue this time: the Printing module's admin-only write actions enforce "system-admin only" in the service (`Forbidden` category), not via an authorization policy, so their 403 genuinely *is* the enveloped body (unlike `TenantsController`'s policy-driven empty-body 403 from S1) — documented explicitly so the distinction doesn't get flattened into a blanket rule. Also found and documented `PrintImagesController.Upload`'s 409, which uniquely returns a *success* envelope (the existing image's metadata) with the status forced to 409, not an `ApiError` — the only non-2xx-without-ApiError response found in the API so far. Backfilled missing `<response>` XML doc tags on all 29 actions across these 5 controllers (same "attributes present but no descriptive text" gap flagged, but not yet fully closed, in S1). **Verification note:** same sandbox constraint — no .NET SDK/NuGet access; every new/edited file was manually cross-checked (brace/paren balance via script, exact codes/messages against `BranchErrors`/`ProductErrors`/`PrintingErrors` and the corresponding `*Service` classes) rather than a real build.
- **2026-08-12 (same day, Swagger documentation Phase S3):** Full example content delivered for Stock & Cards: `StockController`, `ProductItemsController`, `InventoryController` (batch upload), `InventoryHistoryController`, `CardFilesController` (13 endpoints — the smallest phase by count, but the most varied response shapes). Confirmed via `StockService` that `GetByBranch` never 404s on an unknown branch (returns an empty page). Documented `CardFilesController.Generate`'s success response as the second confirmed non-JSON 200 in the API (the file itself, with hand-off metadata in `X-File-Mac`/`X-Card-Count`/`X-Expected-Row-Count` headers — mirrors `PrintImagesController.Get`'s raw-bytes 200 from S2, though that one has no metadata headers to speak of). Continued the enveloped-401-edge-case pattern from S1/S2 on `InventoryController.Upload` (`Batch.ActorNotResolved`) and `CardFilesController.Generate` (`CardFile.ActorNotResolved`). Backfilled missing `<response>` XML doc tags on all 13 actions. **Verification note:** same sandbox constraint — no .NET SDK/NuGet access; every new/edited file was manually cross-checked (brace/paren balance via script, exact codes/messages against `StockErrors`/`ProductItemErrors`/`BatchErrors`/`CardFileErrors` and the corresponding `*Service` classes) rather than a real build.
- **2026-08-12 (same day, Swagger documentation Phase S4):** Full example content delivered for Transfers & Disposal: `TransactionsController`, `CardDisposalsController` (8 endpoints, the most complex business rules in the API — `TransferErrors.cs` alone has 30+ codes). Caught and fixed a real pre-existing doc-comment bug while reading `DisposalService` directly: `CardDisposalsController`'s class doc claimed no read-only admin path exists on this controller at all, but `ResolveReadScope` confirms `GetAll`/`GetById` do give a system admin cross-tenant read access, exactly like `TransactionsController` — only the write path (`Dispose`) rejects an admin token. Also confirmed `Transfer.ActorNotResolved` is, per its own doc comment, unreachable behind `[Authorize]` with a valid tenant token, and documented it as such rather than manufacturing a misleading "reachable" example. Given the size of `TransferErrors.cs`, Swagger examples cover only the most illustrative scenario per response code (e.g. one representative 422 per action, not all ~10 possible ones) — the source file remains authoritative for the full catalogue. Backfilled missing `<response>` XML doc tags on all 8 actions. **Verification note:** same sandbox constraint — no .NET SDK/NuGet access; every new/edited file was manually cross-checked (brace/paren balance via script, exact codes/messages against `TransferErrors`/`DisposalErrors` and the corresponding `*Service`/`ITransferComposer` classes) rather than a real build.
- **2026-08-12 (same day, Swagger documentation Phase S5 — final phase):** Full example content delivered for `BranchRequestsController` (6 endpoints — the seven-status demand-ledger workflow that composes `TransactionsController`'s transfers). Confirmed via `BranchRequestService` that, like `TransactionsController`, `GetAll`/`GetById` give a system admin cross-tenant read access while `Create`/`Confirm`/`Refuse`/`Cancel` all reject an admin token — this controller's own doc comment already stated this accurately, no correction needed (contrast with the `CardDisposalsController` bug found in S4). Documented that `Confirm`'s generated-transfer failures reuse `TransferErrors`/`StockErrors` directly (via `ITransferComposer`, the same validation path a direct `TransactionsController.Create` uses) rather than duplicating codes under a `BranchRequest.*` prefix — its examples mix both prefixes in the same response set for that reason. Backfilled missing `<response>` XML doc tags on all 6 actions. **This closes the Swagger documentation initiative: all 15 controllers / ~90 endpoints now have full example coverage, foundation applies uniformly, and the extension pattern for future endpoints is documented above.** **Verification note:** same sandbox constraint — no .NET SDK/NuGet access; every new/edited file was manually cross-checked (brace/paren balance via script, exact codes/messages against `BranchRequestErrors` and `BranchRequestService`) rather than a real build. A real `dotnet build` across all five phases together is still recommended before considering this initiative fully closed out.

---

## Documentation Metadata

- Last verified against code: 2026-08-12 (Swagger documentation Phase S5 pass — BranchRequestsController, DTOs, service, and error catalogue re-read; other modules not re-verified this pass, see their own phase entries above for last verification)
- Last significant update: 2026-08-12 (Swagger/API documentation initiative complete: S1 foundation + Auth/Tenants, S2 Catalog & Printing, S3 Stock & Cards, S4 Transfers & Disposal, S5 Branch Requests — all 15 controllers covered)
- Maintained by: AI + Project Developers
