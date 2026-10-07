# Evolis Integration — Phase 1 Handoff for Claude Code (local)

This file exists so a local Claude Code session, run from the `inventory` repo root
on the developer's own machine, can pick up this work with full context. It was
produced by a separate Claude session (cloud, no local tooling) that already did a
read-only investigation against the real repos. **Read this entire file before
touching any code.**

## Phase 3 — DONE (2026-10-06), verified live end-to-end

Phase 1, Phase 2, and now Phase 3 (Agent: SDK plumbing, discovery,
self-registration — plan §8) are all complete, in the **other** repo:
`C:\Users\b.zakaria\Desktop\Inventory For MDP\InvetoryServices\invetoryBackGroundServices`
(the Printer Agent, `invetoryBackGroundServices` project, branch `master`).

### Code review (2026-10-06) — three real findings fixed, one confirmed intentional

A `/code-review` run scoped at the Agent repo (not this repo) found four issues;
three were real and fixed, one turned out to be a deliberate, unrelated local
config change the developer confirmed should stay as-is:

1. **`EvolisPrinterAdapter.ToSnapshot` collapsed "unknown" into "false".** When
   `GetStatus` fails (the TCP-link case described below),
   `EvolisDiscoveredPrinterDto.Capabilities`/`SupervisedByEps` are null — but
   `ToSnapshot` was defaulting them to `new PrinterCapabilities(false,false,false,false,false)`
   and `false` respectively before building the vendor-neutral
   `PrinterSnapshot`. That's a confirmed "no" standing in for "we don't know" —
   exactly the distinction the discovery layer's graceful degradation exists to
   preserve, for any future `ICardPrinterAdapter` consumer (e.g. Phase 5's job
   orchestrator deciding whether to use magnetic encoding). Fixed by making
   `PrinterSnapshot.Capabilities`/`SupervisedByEps` nullable too, so the `null`
   now survives all the way through instead of only being nullable on the
   Evolis-specific DTO. (The raw `GetPrinters` controller response was never
   affected — only the `ICardPrinterAdapter` path lost the distinction.)
2. **`EvolisErrors.Cancelled()` was categorized `Internal`, contradicting its own
   doc comment** ("expected outcome of an operator cancel action, not a
   failure"). Internal maps to HTTP 500 — the generic "bug" bucket — which
   would have made a routine operator-initiated cancel (once Phase 5 wires
   `RunJobAsync`) show up in logs/monitoring exactly like a real adapter bug.
   Recategorized to `MachineRejected` (409) — not a perfect semantic fit either,
   but the closest existing category to "the operation didn't happen, and
   that's expected," and categories are a small fixed set (`Validation`,
   `Unauthorized`, `Forbidden`, `CommunicationFailure`, `Timeout`,
   `MachineRejected`, `ProtocolError`, `BackendRejected`, `BackendUnavailable`,
   `Internal`) shared by the whole Agent, not something to extend for one case.
3. **`EvolisMachineController.GetPrinters` was a synchronous action calling
   blocking SDK I/O** (`EvolisDeviceDiscovery.DiscoverAll()` opens a real
   connection and makes blocking calls per device, sequentially). Under
   Angular's stated polling access pattern, multiple concurrent requests each
   blocking a request thread for the full hardware round-trip (up to
   `CommandTimeoutMs`, 30s, per device) risks exhausting the thread pool well
   before real print traffic would. Fixed by making the action `async` and
   wrapping the call in `Task.Run(...)` so the blocking work runs on a
   thread-pool thread instead of tying up the request pipeline.
4. **`appsettings.json`'s `Outbox:ReconciliationIntervalMinutes` was `2`, not the
   shipped default of `30`** — flagged as an unrelated change possibly mixed
   into this diff by accident (this session never touched that file). Asked the
   developer directly: confirmed intentional (a deliberate local tweak,
   unrelated to Phase 3), left as-is.

All three code fixes verified by `dotnet build` (0 errors) after each change.

### Housekeeping from the live end-to-end test — read before touching the DB or this Agent process

- **Tenant `rcdb` (id 3)'s password was changed** to `VerifyPhase3-2026!` during
  testing, to obtain a real print-agent-token. The original password is gone
  (it was only ever a hash) — change it again if this tenant matters.
- **`invetoryBackGroundServices.exe` (the Agent) may still be running** on port
  8403 from this test — it could not be stopped from this session (access
  denied, same permissions issue as the Evolis driver services below). Stop it
  manually if still running: `Stop-Process -Id <pid> -Force`.
- Test printer (`TEST-PHASE3-001`, tenant 3 / branch 3) was created and deleted
  as part of this test — no leftover row.

### The real finding that resolved the Phase-3-pause blocker

The session had previously paused (see the old blocker writeup, kept below for
the record) because `GetStatus()` failed on every attempt against the connected
Primacy 2. Stopping Evolis's driver services (`evoservice`, `EvoPCSvc`,
`evonotify`) and retesting **did not fix it** — ruling out the "driver service
holds the printer" theory. The developer chose to proceed with graceful
degradation (`capabilities: null`, `state: Unknown` whenever `GetStatus` fails,
other fields still populated) rather than keep investigating.

Implementing that way and testing live against the real hardware (which exposes
the same physical Primacy 2 three ways: two `TCP` entries and one `USB` entry)
revealed the actual pattern: **`GetStatus` succeeds over the `USB` link and fails
over `TCP`** — not a global failure, not related to the driver services at all.
Confirmed directly from a real `GET /api/machine/evolis/printers` response:

```json
{"deviceName":"Evolis Primacy2","link":"TCP","capabilities":null,"supervisedByEps":null,"state":"Unknown", ...}
{"deviceName":"Evolis Primacy 2","link":"USB","capabilities":{"magnetic":true,"flip":true,"smart":true,"contactless":true,"ethernet":true},"supervisedByEps":true,"state":"Warning", ...}
{"deviceName":"Evolis Primacy 02","link":"TCP","capabilities":null,"supervisedByEps":null,"state":"Unknown", ...}
```

So the graceful-degradation design turned out to be the right call regardless of
cause: some of this printer's own connection paths genuinely can't produce a
status read on this SDK version, and the discovery endpoint handles that per
device without failing the whole call — exactly as implemented.

### What was built

- `invetoryBackGroundServices.csproj` — `Evolis.SDK` 9.4.1 package reference +
  `<PlatformTarget>x64</PlatformTarget>`. `Evolis.SDK.Native.Core` arrives
  transitively (confirmed via the nuspec).
- `Enums/UsingPrinterType.cs` — created from scratch (this repo had no such enum;
  the pre-existing `Enums/enumType.cs` is unrelated). Mirrors the Inventory
  API's values (`Evolis = 0, Matica = 1`).
- `Options/EvolisOptions.cs` — section `"Evolis"`, matches the plan's table.
- `Machine/Abstractions/{PrinterSnapshot,CardJob,ICardPrinterAdapter}.cs` — the
  vendor-neutral seam (plan §4.3). `RunJobAsync`/`CardJob`/`CardJobResult` are
  shape-only, same as the plan says — real implementation is Phase 5.
- `Machine/Evolis/EvolisErrors.cs` / `EvolisExceptions.cs` — mirrors
  `MachineErrors`'s factory shape; same `MachineError` type, same resx-key
  convention.
- `Machine/Evolis/EvolisStatusMapper.cs` — **built from the real `ReturnCode`
  and `Status.Flag` enum values confirmed by reflecting over the installed
  `Evolis.SDK` 9.4.1 assembly**, not the plan's draft list (the plan names far
  fewer codes than the SDK actually has — entire `HTTP_*`/`SVC_*`/`SANDBOX_*`/
  `SMART_*`/`CARD_*`/`LAM_*` families exist that the plan never mentions; all
  of those fall through to `Internal` via the exhaustive switch's discard arm,
  per plan rule §13.4 #7). Also found live: `Status.CfgFlag`/`InfFlag`/
  `WarFlag`/`ErrFlag` are **obsolete** in this SDK version, superseded by one
  unified `Status.Flag` — the mapper uses the current API.
- `Machine/Evolis/EvolisConnectionFactory.cs` / `EvolisConnectionScope.cs` —
  open-per-operation (never pooled), configures `OpenMode`/`ErrorManagement`
  best-effort (confirmed live that `SetErrorManagement` can itself fail
  independently of the connection opening — logged, never thrown), optional
  `Reserve()` only when requested (never during discovery).
- `Machine/Evolis/EvolisDeviceDiscovery.cs` — calls `Evolis.GetDevices()`, then
  briefly opens each device read-only to read status/ribbon, with the
  graceful-degradation behavior described above.
- `Machine/Evolis/EvolisPrinterAdapter.cs` — `ICardPrinterAdapter` for Evolis;
  discovery/status only this phase.
- `Services/{IEvolisSupervisorRegistrationClient,EvolisSupervisorRegistrationClient}.cs`
  — typed `HttpClient` to `POST api/printers/supervisor-registration`, same
  pattern as the pre-existing `ReconciliationTokenProvider`.
- `Services/EvolisSupervisorRegistrar.cs` — `IHostedService`; reads the
  `branchId` claim directly off the freshly-minted reconciliation JWT (decoded
  locally, not re-derived from config) rather than duplicating it in
  `EvolisOptions`. Runs at startup (`AutoRegisterSupervisorOnStartup`) and again
  after every manual discovery call. Never throws — every failure path is
  logged only, confirmed live (plan §8 acceptance criteria: an unreachable
  Inventory API at startup must not block the Agent).
- `Params/EvolisDiscoveryDtos.cs` — response DTOs, nullable
  `capabilities`/`supervisedByEps`/ribbon for the degradation case.
- `Controllers/EvolisMachineController.cs` — `GET /api/machine/evolis/printers`,
  `[Authorize(Policy = PrintAgentAuthPolicy.Name)]`, triggers
  `EvolisSupervisorRegistrar.RegisterNowAsync` (fire-and-forget,
  `CancellationToken.None` so it isn't cut off when the HTTP response returns).
- `Messages.resx` / `Messages.ar.resx` — all `Evolis.*` error keys, EN + Egyptian
  Arabic.
- `Program.cs` — full DI wiring: `EvolisOptions`, `EvolisConnectionFactory`/
  `EvolisDeviceDiscovery` (singletons — stateless, SDK-call-only),
  `AddKeyedScoped<ICardPrinterAdapter, EvolisPrinterAdapter>(UsingPrinterType.Evolis)`,
  the registration client's typed `HttpClient`, `EvolisSupervisorRegistrar`
  registered once as a singleton and exposed as the `IHostedService` (so the
  controller calls the same instance the host drives), and
  `Evolis.SetLogPath`/`SetLogLevel` from config at startup.

One naming note: this repo's own namespace is `invetoryBackGroundServices.Machine.Evolis`,
which collides with the SDK's own `Evolis` root namespace — call sites that need
`Evolis.Evolis.*` (the SDK's static class) from inside that namespace must say
`global::Evolis.Evolis.*` or the compiler resolves `Evolis` as the enclosing
namespace instead.

### Verified live, step by step

1. `dotnet build` on the Agent: 0 errors.
2. Provisioned a throwaway test printer (tenant 3 / branch 3, `TEST-PHASE3-001`,
   `DeviceName: "Evolis Primacy 2"` — matching the real USB device's SDK name)
   via the real Inventory API.
3. Confirmed the pre-existing reconciliation service account
   (`542fb65a-...`, tenant 3 / branch 3) still mints a real token via
   `POST /api/auth/service-token`.
4. Obtained a real print-agent-token via `POST /api/auth/print-agent-token`
   (required resetting the tenant's password first — see housekeeping above).
5. Ran the real Agent (`dotnet run`, port 8403) against the real Inventory API
   (`https://localhost:7037`, launch profile `https`, since
   `InventoryApi:BaseUrl` is HTTPS).
6. **Startup log showed `EvolisSupervisorRegistrar` firing automatically**,
   minting a token and posting to `supervisor-registration` — both `200 OK`.
7. Called the real `GET /api/machine/evolis/printers` with the real
   print-agent-token: `200 OK`, all three real discovered devices returned,
   degradation confirmed exactly as described above.
8. Queried the Inventory API's database directly: `SupervisorHostName =
   "L-DEV-BELAL"` and a real `SupervisorRegisteredAt` timestamp, confirming the
   self-registration round-trip actually persisted.

### Old blocker writeup (superseded by the finding above, kept for the record)

### What's actually done so far in that repo

- `invetoryBackGroundServices.csproj` — added `<PackageReference Include="Evolis.SDK"
  Version="9.4.1" />` and `<PlatformTarget>x64</PlatformTarget>`.
  `Evolis.SDK.Native.Core` is a transitive dependency (confirmed via the nuspec), not
  added explicitly.
- `Enums/UsingPrinterType.cs` — **created from scratch**. This repo had no such enum
  at all before (the only pre-existing `Enums/enumType.cs` is unrelated —
  `Success/Warning/Error/Info`). Mirrors the Inventory API's
  `DomainLayer.Enums.UsingPrinterType` values (`Evolis = 0, Matica = 1`).
- `Options/EvolisOptions.cs` — created, section `"Evolis"`, matching the plan's table
  exactly (`OpenMode`, `ErrorManagement`, `ReserveOnJob`, timeouts,
  `AutoRegisterSupervisorOnStartup`, native log settings).
- `Machine/Abstractions/PrinterSnapshot.cs`, `CardJob.cs`, `ICardPrinterAdapter.cs` —
  created. The vendor-neutral seam the plan calls for (§4.3): no `Evolis.*` type
  crosses out of `Machine/Evolis/`. `RunJobAsync`/`CardJob`/`CardJobResult` are shape
  only in this phase, same as the plan says — real implementation is Phase 5.

**Not yet started:** `Machine/Evolis/*` (connection factory/scope, device discovery,
status mapper, errors, exceptions, the actual `EvolisPrinterAdapter`),
`Services/*` (supervisor-registration HTTP client + `IHostedService` registrar),
`Params/EvolisDiscoveryDtos.cs`, `Controllers/EvolisMachineController.cs`,
`Messages.resx`/`Messages.ar.resx` entries, and the `Program.cs` DI wiring. None of
these exist yet in that repo.

### Why it's paused — a real hardware finding that contradicts the plan

The plan's discovery endpoint design depends on `Connection.GetStatus()` for two
response fields (`capabilities.*` and `supervisedByEps`), and explicitly says
discovery should **never** call `Reserve()` (it would lock the LCD and disrupt a
walk-up operator).

Live-tested against the real connected Evolis Primacy 2 on this machine (not
guessed — a throwaway probe project under `%TEMP%\evolis_probe3`, same approach as
the earlier Task 2 probes): **every attempt to read live status failed**, by every
method tried — `GetStatus()`, `SendStatusRequest()`, `EnableStatus()`, and
`Reserve()` itself (tried on both the USB and TCP device entries, with
`SetSessionManagement(false)` tried too) — all returned `false`, with
`PRINTER_ENOCOM` or `EUNDEFINED`. In contrast, `GetInfo()` and `GetRibbonInfo()`
succeeded every single time, with no special handling needed.

Also found: this machine has Evolis's own driver/print-center services running —
`evoservice`, `EvoPCSvc`, `evonotify`, `ElevocControlService` (confirmed via
`Get-Process`). The working theory (not yet confirmed from documentation) is that
one of these holds the printer in a way that blocks any other client — including
this SDK session — from reserving it or reading live status, even though
`Device.isSupervised` reports `false` for all three discovered device entries.

This is exactly one of the plan's own originally-flagged "unknowns pending
hardware" (§3: *"Does `Reserve()` succeed while `INF_PCSVC_LOCKED` is set?"*) — the
real answer, on this machine, right now, is **no**. Separately from `Reserve()`,
even a non-reserving status read fails outright here, which the plan did not
anticipate at all (its design assumes a non-reserving status read always works).

### Open decision — ask the developer before resuming

Three options were presented; the developer stopped the session before picking one
(said to pause and write this status down first, so **do not assume an answer and
do not proceed** — ask again when resuming):

1. Stop `evoservice`/`EvoPCSvc`/`evonotify` temporarily (reversible — just restart
   them after) and re-test whether `GetStatus` starts working with them stopped.
   This would confirm or rule out the "driver service holds the printer" theory.
2. Proceed without real `capabilities`/`supervisedByEps` data: apply plan rule §13.4
   #7 ("an unmapped SDK code degrades to Internal + log, never throws") to this
   case too — return `capabilities: null`, `state: Unknown` whenever `GetStatus`
   fails, while `deviceName`/`model`/`ribbon.sdkCode` (which do work) are still
   populated. Document the limitation here.
3. Research further (Evolis SDK docs / support) before deciding either way.

The probe project used for this investigation is still at
`C:\Users\b.zakaria\AppData\Local\Temp\evolis_probe3` if it needs to be re-run.

### Build state

`dotnet build` on `invetoryBackGroundServices.csproj` succeeds with the four files
created so far (0 errors, only pre-existing warnings unrelated to this work). Not
yet run against the real Agent process — no controller/endpoint exists yet for
this phase, so there's nothing to exercise live beyond what the probe already
confirmed directly against the SDK.

### Resuming

When picking this back up: re-ask the three-option question above first — don't
assume which one was intended. Then continue down the plan's file list (§8) for
whichever direction is chosen.

## Status as of 2026-10-04 — both tasks below are RESOLVED

Both Task 1 (`RibbonTypes.SdkCode`) and Task 2 (`EvolisTray`) described below have
been completed in a local session, verified against the real dev database and (for
Task 2) real connected Evolis Primacy 2 hardware. The original text of both tasks is
kept below for historical context (it explains *why* each decision was made), but
treat this status section as authoritative over anything below that it contradicts.

### Task 1 outcome — not what the handoff assumed

The handoff's premise ("the developer has already written the three-step
nullable-add → backfill → `NOT NULL` fix") was **false** — the migration on disk at
session start was still the original single-step, non-nullable `AddColumn` the
background section warns about. That rewrite was never needed, though: a live check
showed `RibbonTypes` was genuinely empty (no manually-inserted rows, contradicting
the handoff's own inference), so there was no backfill/unique-index conflict to
guard against. The original migration
(`20261001094229_EvolisPrinterConfiguration .cs`) was left untouched.

What was actually missing was seed data. Real `Evolis.RibbonType` values were
obtained by reflecting over the installed `Evolis.SDK` 9.4.1 assembly
(`evolis-sdk-cs.dll`, found via `dotnet nuget locals global-packages --list`) — it
*is* a real C# enum in this SDK version (contradicting the handoff's note that it
wasn't), so `Enum.GetNames` worked directly, no decompiler needed. `KWHITE` was
additionally confirmed live against a connected Evolis Primacy 2 via
`Connection.GetRibbonInfo()`. Seeded via a new migration
(`20261004102226_SeedRibbonTypes.cs`, generated with `dotnet ef migrations add`) and
an `entity.HasData(...)` block in `AppDbContext.ConfigureProductPrintConfigurations`:
10 rows — `YMCKO`, `YMCK`, `YMCKOK`, `KBLACK`, `KWHITE`, `KRED`, `KBLUE`, `KGREEN`,
`HOLO`, `CLEAR` — a curated common set, not the full 32-member enum and not a
Primacy-2-only list (the SDK has no "ribbons this model supports" API; it only
reports what's currently loaded).

### Task 2 outcome — redesigned, not just filled in

The handoff was right that this needed real SDK evidence, but the actual shape was
different from what it anticipated. Reflecting over the SDK assembly found:
`Evolis.InputTray` and `Evolis.OutputTray` are two distinct real enums (not one
shared tray enum), and `Connection.GetErrorTray()`/`SetErrorTray(Evolis.OutputTray)`
are themselves typed `Evolis.OutputTray` — i.e. `ErrorTray` uses the `OutputTray`
type, not a third type. Both were confirmed live against a connected Primacy 2.
Values are bit-flag-shaped in the SDK (powers of two, shared across every Evolis
model), but a live test setting a combined value (`FEEDER | BEZEL`) on the Primacy 2
was rejected with `EUNSUPPORTED` — this model only ever holds one value at a time, so
no `[Flags]` bitmask semantics were implemented, just wider storage.

`DomainLayer/Enums/EvolisTray.cs` was deleted and replaced by `InputTray.cs` and
`OutputTray.cs` with the real SDK values. `EvolisPrinterConfiguration.InputTray` is
now type `InputTray`; `OutputTray`/`ErrorTray` are now type `OutputTray`. Same change
propagated to `EvolisPrinterConfigRequest`/`EvolisPrinterConfigResponse` in
`PrintingDtos.cs`. `AppDbContext` now uses `HasConversion<short>()` (not `<byte>`) for
these three columns, because `OutputTray.LOCKED = 256` doesn't fit in a `tinyint`.
Migration `20261004122849_FixEvolisTrayTypes.cs` widens the columns from `tinyint` to
`smallint` — and, caught in code review, also contains explicit `Sql()`
`UPDATE ... CASE` statements (both directions, `Up()` and `Down()`) translating any
pre-existing row's old shared-enum numeric value to the new per-column scheme, since
the two schemes share no numbers and a plain `AlterColumn` alone would have silently
changed the meaning of any row that already existed (none did in this dev DB, but the
bug would have hit the first environment that had rows before this migration ran).

All of the above was verified against the real `InventoryDb` dev database: both new
migrations applied cleanly, rows/columns inspected directly via `sqlcmd`, and both
reversed cleanly (`dotnet ef database update <previous-migration>`) and reapplied.

### Phase 1 plan-vs-reality audit (2026-10-05) — two real gaps found and fixed

After both tasks above were done, the full `Evolis-Integration-Plan.md` §6 (Phase 1)
was read end-to-end and checked line-by-line against the actual code. Most of it
matched (schema, CRUD endpoints, error codes incl. EN/AR localization). Two real
problems were found and fixed; a few more are deliberate, already-documented
deviations (not bugs) kept for the record below.

**Fix 1 — validation bug in `PrinterConfigurationService.cs` (`CreateAsync` +
`UpdateAsync`).** A complete method, `ValidatePrinterConfigPayloadShape`, already
existed to check both directions (Matica needs `MaticaConfig` and rejects
`EvolisConfig`; Evolis needs `EvolisConfig` and rejects `MaticaConfig`) — its own doc
comment even said "Shared by `CreateAsync` and `UpdateAsync`" — but neither method
actually called it. Both instead called `ValidateMaticaPayloadShape`, which only
checks the Matica side. Consequence: `POST /api/printers` with
`usingPrinterType: 0` (Evolis) and no `evolisConfig` at all skipped straight to
`request.EvolisConfig!.DeviceName` (a null-forgiving operator, not a null check) and
threw a `NullReferenceException` → 500, instead of the intended `422
Printer.EvolisConfigRequired`. Fixed by swapping both call sites to
`ValidatePrinterConfigPayloadShape` and deleting the now-dead
`ValidateMaticaPayloadShape`. **Verified live**, not just by reading the diff: ran
the real API locally, logged in as the seeded system admin, and confirmed
`POST /api/printers` with `usingPrinterType: 0` and no `evolisConfig` now returns
`422 Printer.EvolisConfigRequired` (was 500 before the fix), while a real create with
a valid `evolisConfig` still returns `200` and persists `InputTray=1, OutputTray=1,
ErrorTray=8` correctly (confirmed via `sqlcmd` against the live row, id 2, then
deleted as test cleanup).

**Fix 2 — missing endpoint.** The plan explicitly lists
`GET /api/ribbon-types → RibbonTypeResponse gains SdkCode` as a Phase 1 deliverable.
No controller or route for it existed anywhere in the repo. Added, following the
codebase's existing `IServiceManager` façade pattern exactly:
- `ApplicationLayer/ServicesContracts/IRibbonTypeService.cs` (new) — one method,
  `GetAllAsync`, no paging/filtering (it's a small global lookup table, not
  tenant-scoped, read-only).
- `InfrastructureLayer/Services/RibbonTypeService.cs` (new) — thin wrapper over
  `IUnitOfWork.RibbonTypes.GetAllAsync()`, maps to `RibbonTypeResponse`.
- `IServiceManager.cs` / `ServiceManager.cs` — added `RibbonTypes` property, same
  `Lazy<T>` pattern as every other service on the façade.
- `InfrastructureServiceRegistration.cs` — registered the new service.
- `InventoryManagmentAndInstanceIssuancePresentationLayer/Controllers/RibbonTypesController.cs`
  (new) — `[Authorize]`, `GET /api/ribbon-types`, open to both tenant and admin
  callers (same reasoning as the rest of this reference table: global, not
  tenant-scoped, read-only).

**Verified live**: ran the real API, `GET /api/ribbon-types` with a valid bearer
token returns all 10 seeded rows with their `sdkCode`.

**Deliberate deviations from the plan's literal text — not bugs, already decided
earlier in this session, listed here only so the gap between plan and code is fully
accounted for:**
- Plan said one shared `EvolisTray.cs` enum (`tinyint`). Code has `InputTray.cs` /
  `OutputTray.cs` as two distinct enums (`smallint`) — see the Task 2 section above
  for why (real SDK evidence, confirmed live against hardware).
- Plan's `RibbonTypes` seed list (`YMCKO, YMCKOK, YMCKOO, YMCK, YMCKK, YMCKOS,
  KBLACK, KBLACK_R, KSCRATCH` — itself marked "partial", deferring to Phase 0's
  final list) differs from what was actually seeded (`YMCKO, YMCK, YMCKOK, KBLACK,
  KWHITE, KRED, KBLUE, KGREEN, HOLO, CLEAR`) — an explicit developer choice made
  earlier in this session, not an oversight.
- Plan said the ribbon seed belongs in `DbSeeder.cs` (a runtime seed). It was
  implemented as `HasData()` in the `SeedRibbonTypes` migration instead
  (migration-time seed) — an alternative, equally valid approach, just not the file
  the plan named.

### `id: 0` / stale-field bug — root-caused, found in 3 services, all 3 fixed (2026-10-05)

The `id: 0` / `createdAt: "0001-01-01..."` symptom noted above was root-caused and
fixed, not left open. Root cause, exactly: `UnitOfWork.ExecuteInTransactionAsync<T>`
(`InfrastructureLayer/UnitOfWork.cs`) runs the caller's lambda (`work()`) *first*,
and only calls `_context.SaveChangesAsync(...)` *after* that lambda returns. Any
service that builds its response DTO — via a `Map(...)` call reading
`entity.Id`/`entity.CreatedAt`/`entity.UpdatedAt` — **inside** that lambda is reading
those fields before EF Core has generated the identity or
`AppDbContext.StampAuditFields()` has stamped the audit columns. The row ends up
correctly persisted (confirmed directly against the database both times); only the
in-memory response object is stale.

This exact failure mode was already known and already correctly solved elsewhere in
this codebase — `DisposalService.cs` and `TransferService.cs` both carry an explicit
comment (`"disposal.Id is 0 until SaveChanges runs, which happens after the lambda
returns"`) and declare their entities *outside* the transaction lambda, building the
response only after `ExecuteInTransactionAsync` returns. `BranchRequestService` and
`BatchUploadService` follow the same shape. All eight call sites of
`ExecuteInTransactionAsync` across the services layer were audited; three had not
applied this pattern and were fixed to match it:

- `PrinterConfigurationService.CreateAsync` / `UpdateAsync` — `printer`,
  `maticaConfig`, `evolisConfig` now declared before the transaction; the lambda
  returns a plain (non-generic) `Result`; `Map(...)` runs after, only once
  `transactionResult.IsFailure` is checked.
- `ProductService.CreateAsync` — same shape, for `product`.
- `PrintImageService.UploadAsync` — same shape, for `printImage`.

`ProductPrintConfigurationService.UpdateForProductAsync` was checked and left
alone — it already builds its response from the in-memory staged entities after
validating inside the lambda (a different, already-correct variant of the same
principle, also documented with its own comment), not from a fresh query.

**Verified live** against the real running API (not just by reading the diff): for
each of the three fixed services, created a real row and confirmed the response now
returns the true generated `id` and `createdAt` (and, for the printer update case,
a real `updatedAt` instead of `null`) — then deleted the test rows afterward.

## Phase 2 — Supervisor Self-Registration (done, 2026-10-05)

Implemented `POST /api/printers/supervisor-registration` (Evolis Integration Plan
§7, decisions Q-21/Q-28): lets the Printer Agent write `SupervisorHostName` /
`SupervisorRegisteredAt` on the Evolis printers it actually discovers, under its
existing reconciliation service-account token — no new write surface reachable by
a tenant/admin session.

**One deliberate deviation from the plan's literal text, agreed with the developer
before writing any code.** The plan says *"Add scope `printer:register-supervisor`
to the service-account model."* Inspecting the actual code
(`PrintAgentServiceAccount`, `AuthService.CreateServiceTokenAsync`,
`AuthorizationPolicies`) found **no scope concept exists anywhere in this
system** — a service account mints exactly one kind of token
(`ReconciliationTokenGenerator`, `PurposeClaim = "reconciliation"`), scoped to
exactly one `(TenantId, BranchId)` pair fixed at provisioning time. There was never
a multi-capability model to extend.

The developer's own framing resolved it: every printer belongs to exactly one
branch, and a service account is already permanently bound to exactly one branch —
so the real protection this endpoint needs (an Agent for branch X can never touch
branch Y's printers) is already structurally guaranteed by the account itself,
with no scope needed. Implemented as: the new action uses the existing
`AuthorizationPolicies.ReconciliationOnly` policy as-is (no new scope, no schema
change to `PrintAgentServiceAccount`), plus the same defense-in-depth cross-check
`PrintFlowController.IsOutsideTokenScope` already uses — the request body's
`BranchId` must match the token's own `branchId` claim, or `403
Printer.SupervisorBranchMismatch`. This still satisfies decision Q-28's actual
intent ("reuse the existing credential, don't mint a fourth key") — it just skips
a scope layer that would have had nothing to scope.

**What was added:**
- `ApplicationLayer/DTOs/Printing/PrintingDtos.cs` — `DiscoveredDeviceDto`,
  `SupervisorRegistrationRequest`, `MatchedSupervisorDevice`,
  `UnmatchedDiscoveredDevice`, `UnseenRegisteredPrinter`,
  `SupervisorRegistrationResponse`.
- `ApplicationLayer/Errors/PrintingErrors.cs` — `SupervisorBranchMismatch` (403),
  `SupervisorHostNameRequired` (422).
- `ApplicationLayer/Contracts/IEvolisPrinterConfigRepo.cs` +
  `InfrastructureLayer/Repositories/EvolisPrinterConfigRepo.cs` —
  `GetByTenantAndBranchAsync`: loads every registered, non-deleted Evolis config
  for a branch in one query (keyed by `DeviceName`), not just a lookup on the
  reported devices — needed for the `registeredButNotDiscovered` partition, which
  must know about printers the Agent's report didn't mention at all.
- `ApplicationLayer/ServicesContracts/IPrinterConfigurationService.cs` +
  `InfrastructureLayer/Services/PrinterConfigurationService.cs` —
  `RegisterSupervisorAsync`: the only method on this interface with no
  system-admin check (by design — it's reached exclusively via the Agent's
  service-account token). Does the three-way partition (matched / unmatched /
  registeredButNotDiscovered), logs a warning when overwriting an existing
  different `SupervisorHostName` (added an `ILogger<PrinterConfigurationService>`
  dependency to the constructor for this), and never auto-creates a printer for an
  unmatched device.
- `InventoryManagmentAndInstanceIssuancePresentationLayer/Controllers/PrintersController.cs`
  — `RegisterSupervisor` action, `[Authorize(Policy =
  AuthorizationPolicies.ReconciliationOnly)]`, plus its own
  `IsOutsideTokenScope` branch check before ever calling the service.
- `Messages.resx` / `Messages.ar.resx` — the two new error keys, EN + Egyptian
  Arabic.

**Verified live** against the real running API, not just by reading the diff:
provisioned a real reconciliation service account via
`POST /api/auth/service-accounts`, minted a real service token via
`POST /api/auth/service-token`, registered a real Evolis printer, then confirmed
every case against the actual endpoint:
- A real discovered device matches a registered printer → `matched`, `updated:
  true`, `SupervisorHostName` written.
- Re-posting the identical payload → `matched`, `updated: false` (idempotent).
- An unregistered device name → `unmatched`, `reason: "NotRegistered"`, no printer
  created.
- Reporting zero devices for a branch with a registered printer →
  `registeredButNotDiscovered` lists it.
- Request body `branchId` not matching the token's own branch → `403
  Printer.SupervisorBranchMismatch`.
- Blank `hostName` → `422 Printer.SupervisorHostNameRequired`.
- A system-admin session token (wrong token type entirely) against this endpoint →
  `401`, confirming the policy's authentication-scheme restriction actually holds.

All test rows (printer, service account) deleted afterward; no leftover state in
the real `InventoryDb`.

### Code review caught a real bug — fixed and re-verified (2026-10-05)

A `/code-review` pass on Phase 2 found one real, confirmed bug:
`EvolisPrinterConfigRepo.GetByTenantAndBranchAsync` originally returned
`rows.ToDictionary(e => e.DeviceName)`. But `DeviceName` is **not** unique per
branch — `AppDbContext.cs` lines 936-939 say so explicitly in a comment of its own:
*"one physical device name may legitimately repeat across different supervising
machines"* — the real unique index is on `(TenantId, SupervisorHostName,
DeviceName)`, not `DeviceName` alone. Two registered Evolis printers sharing a name
(e.g. two counters whose printers both report the driver's default display name)
would make `ToDictionary` throw `ArgumentException` and crash reconciliation for
the *entire branch*, not just the duplicated printer. Even without a crash, the
matching logic resolved a reported device name to a candidate "by name" with no
regard for which host actually owns it — a report from a second host could have
silently reassigned a printer's `SupervisorHostName` away from its real owner.

Fixed by changing the repo method to return a flat `IReadOnlyList<...>` (interface
and implementation both updated) and doing the matching in
`PrinterConfigurationService.RegisterSupervisorAsync` by grouping candidates by
name, then picking: a candidate this exact host already supervises (idempotent
re-affirm) → else a candidate no host has claimed yet (first registration) → else
none of the above, which now reports `unmatched` with a new reason,
`"ClaimedByDifferentHost"`, instead of ever reassigning someone else's printer.
`registeredButNotDiscovered` now tracks matched printer ids, not device names, so
it stays correct when two printers share a name.

**Verified live**, not just by reading the diff: created two real Evolis printers
in the same branch with the identical `DeviceName`, then walked all three hosts
through the real running endpoint —
1. Host A reports the name → claims printer 5 (the first unclaimed match); printer
   6 reported as `registeredButNotDiscovered`.
2. Host B reports the same name → claims printer 6 (the *other* unclaimed one, not
   printer 5); printer 5 correctly still shows as `registeredButNotDiscovered`
   from Host B's perspective.
3. Host C reports the same name → both printers are now claimed by other hosts, so
   it gets `unmatched`/`"ClaimedByDifferentHost"` — confirmed it never steals
   either one, and confirmed no crash anywhere in the sequence.

Test printers and service accounts deleted afterward.

### Second code-review pass caught another real bug — in an unrelated old migration (2026-10-06)

A `/code-review` run (intended for the Agent repo's Phase 3 work, but it reviewed
the current working directory's own diff, which was this repo) found a real bug in
`20261004122849_FixEvolisTrayTypes.cs`'s `Down()`: it called `AlterColumn<byte>`
(narrowing `smallint` → `tinyint`) **before** the `Sql()` `UPDATE ... CASE`
translation, the reverse of `Up()`'s correct translate-then-resize order. Any row
with `OutputTray = OutputTray.LOCKED (256)` — the exact value that originally
justified needing `smallint` — would make that `AlterColumn` try to cast `256` into
a `tinyint` (max 255) before the translation ever got a chance to bring it into
range, which SQL Server rejects outright with an arithmetic-overflow error. Fixed
by moving the `Sql()` block before all three `AlterColumn` calls in `Down()`,
mirroring `Up()`'s order.

**Verified live**: created a real Evolis printer, set its `OutputTray` directly to
`256` via `sqlcmd`, ran `dotnet ef database update <previous-migration>` — before
the fix this would have thrown; after the fix it completed cleanly and the column
correctly held `2` (`STANDARD`, the `ELSE` fallback for an old-scheme value with no
counterpart) once narrowed back to `tinyint`. Re-applied forward afterward; test
printer deleted.

## Phase 4 — Agent: Card Rendering with SkiaSharp (DONE, 2026-10-06)

Implemented `ICardRenderer`/`SkiaCardRenderer` (plan §9): a pure, hardware-free
function from a cardholder name + flattened `EvolisProductPrintConfiguration`
fields to a BMP byte array, plus the `CardPanel` (`ResinBlack`/`Colour`) decision
Phase 5's ribbon-compatibility check will consume.

**Decision made with the developer before writing any code**: the plan says "test
it hard" with a long list of specific unit tests, but neither repo has a test
project at all yet — adding one for the first time is itself an architectural
choice. Asked directly; developer chose to skip a formal test project and verify
with a real, live scratch program instead (same approach already used for the
Evolis SDK probes earlier in this file) — faster, but means this phase has no
permanent regression safety net the way a committed test suite would.

**A real finding from building this, not guessed:** confirmed by reading the
installed SkiaSharp 2.88.8 XML docs that `SKImage.Encode(SKEncodedImageFormat, int)`
does not support `Bmp` as an output format in this version — `SKEncodedImageFormat.Bmp`
exists only for *decoding* (`SKCodec`) existing BMP files; Skia's own encoder writes
Png/Jpeg/Webp only. Since the plan requires BMP output (decision Q-23), `SkiaCardRenderer`
hand-writes the BMP file header, DIB header, and pixel data directly from the
rendered `SKBitmap` — two private methods, one for 24-bpp BGR (`CardPanel.Colour`)
and one for 1-bpp black/white with a 2-entry palette (`CardPanel.ResinBlack`).

**What was built:**
- `invetoryBackGroundServices.csproj` — `SkiaSharp` + `SkiaSharp.NativeAssets.Win32` 2.88.8.
- `Enums/{PrintWay,PrintedFace}.cs` — created from scratch, mirroring the
  Inventory API's `DomainLayer.Enums` values exactly (this repo had neither).
- `Options/EvolisOptions.cs` — added `DefaultFontFamily` (default `"Arial"`) and
  `MonochromeThreshold` (default `32`, decision Q-25).
- `Machine/Rendering/{CardGeometry,CardPanel,CardRenderRequest,CardRenderResult,ICardRenderer,SkiaCardRenderer}.cs`
  — exactly the plan's file list. `CardRenderRequest` deliberately omits
  `RibbonTypeId`/`ImageId` from the full `EvolisProductPrintConfiguration`: the
  renderer has no printer (ribbon compatibility is Phase 5's concern, consuming
  `CardRenderResult.Panel`) and no image store (a background image is out of this
  phase's scope).
- `Program.cs` — `AddSingleton<ICardRenderer, SkiaCardRenderer>()`.

**Verified live** via a real scratch console program (project-referencing the
actual six `Machine/Rendering` + `Enums`/`Options` files, not a copy) — all 15
checks the plan's test list calls for passed:
- Landscape → 1016×648; portrait → 648×1016.
- Output starts with the real BMP signature (`0x42 0x4D`); DIB header dimensions match.
- `#000000` → `ResinBlack`; `#FF0000` → `Colour`; `#1A1A1A` → `ResinBlack` (within
  the default threshold 32, since `0x1A = 26 ≤ 32`).
- `12pt` at 300 dpi → `50px` exactly.
- An unknown font family substitutes `DefaultFontFamily` and emits a warning (not silent).
- An out-of-canvas `(X, Y)` is clamped and emits a warning.
- A non-white background on a resin panel is forced white and emits a warning.
- A very long cardholder name does not throw.
- The 1-bpp BMP's palette has exactly two entries, black then white.
- Rendered the actual BMP bytes to real files and visually inspected them
  (converted to PNG via ImageMagick to view): landscape colour panel renders
  red text on white, landscape resin panel renders black text on white,
  portrait geometry is correctly rotated — all exactly as configured.

**Known limitation found during live verification, not covered by the plan's own
test list:** an Arabic cardholder name renders without throwing and produces
non-blank pixels (satisfying the plan's literal "RTL shaping sanity" requirement),
but visually the glyphs are laid out left-to-right with no bidi reordering or
Arabic shaping — `SKCanvas.DrawText` does neither on its own; that needs
`HarfBuzzSharp` or manual bidi handling, which is not in this phase's file list or
either Phase 4/5 plan text. If Arabic names are printed for real before that's
addressed, they will render backwards. Flagged here for a future phase/decision,
not fixed in this one.

Scratch verification project and its output files deleted after use — nothing
left in the Agent repo beyond the files listed above.

### Follow-up, same day: Arabic names blocked outright, English names forced uppercase

After seeing the RTL-shaping limitation above, the developer's decision: Arabic
names are **not supported at all right now** (will become a per-product config
choice in a separate future phase) — for now, every cardholder name must be
English and is forced to CAPITAL letters.

Added `Machine/Rendering/InvalidCardHolderNameException.cs` and a new
`ValidateAndNormalizeCardHolderName` step at the top of `SkiaCardRenderer.Render`:
rejects (throws, does not degrade) any character outside ASCII letters/digits/
`. , - '`/space, and uppercases the result otherwise. This is the one case where
`ICardRenderer.Render` throws — every other input (missing font, out-of-bounds
position, unparseable color) still degrades-and-warns per the plan's own rule;
a non-English name is a hard business rule, not a layout quirk, so it's rejected
outright instead of silently mis-rendering RTL text. `ICardRenderer`'s doc
comment updated to say so explicitly.

**Verified live** (same scratch-project approach, re-run after this change):
Arabic input now throws `InvalidCardHolderNameException` with a clear message
instead of rendering backwards; a lowercase English name renders with no
exception; punctuation (`O'Brien-Smith Jr.`) and digits (`John 3rd`) are
accepted; an empty name is rejected; a plain English name still renders at the
correct geometry (regression check). Visually confirmed the uppercase rule
itself by rendering `"john smith"` and converting the output BMP to PNG: the
printed text reads `"JOHN SMITH"`.

## Phase 5 — Agent: Evolis Print Flow (done, 2026-10-07)

Implemented the Evolis counterpart to the Matica `Print-Card-Holder-Name` flow
(plan §10): `POST api/Machine/Print-Card-Holder-Name-Evolis`, Print-Agent-token
protected, running the twelve-step job (gate → ready check → connect+reserve →
mag read → resolve-for-print → ribbon check → render → print → local batch →
eject/reject → record-print-result → release+dispose).

**Developer explicitly asked for stronger-than-planned verification.** The plan
itself says Phase 5 needs "no hardware" (a fake-adapter unit-test level is
enough; real hardware is Phase 6). Asked the developer directly whether to
honor that or go further now — chose to verify against the real connected
Evolis Primacy 2 and the real running Inventory API *now*, not defer it. Three
real SDK findings came out of that choice that a fake adapter would never have
caught (see below) — concrete evidence for why the developer's instinct here
was right.

### Real SDK findings — none of these were guessed, all confirmed live or from the installed SDK's own XML docs

1. **`SettingsKey.GRibbonType`'s string vocabulary is not `RibbonTypes.SdkCode`.**
   `PrintSession.SetSetting(GRibbonType, "KWHITE")` (the plain name seeded in
   Phase 1) does nothing — confirmed live, returned `false`. The SDK's own XML
   docs (`F:Evolis.SettingsKey.GRibbonType`, "Possible values") list a
   completely different, prefixed vocabulary: `RC_*` for colour ribbons
   (`RC_YMCKO`, `RC_YMCK`, `RC_YMCKOK`, …), `RM_*` for monochrome/specialty ones
   (`RM_KBLACK`, `RM_KWHITE`, `RM_KRED`, `RM_KBLUE`, `RM_KGREEN`, `RM_HOLO`, …).
   `Machine/Evolis/EvolisPrintSettingsMapper.cs` carries an explicit, live-verified
   lookup table for exactly the 10 ribbons Phase 1 seeded.
   **`CLEAR` (`Evolis.RibbonType.CLEAR = 2000`) has no `GRibbonType` token at
   all** — tried `"CLEAR"`, `"RM_CLEAR"`, `"RC_CLEAR"`, all rejected. A product
   configured with the `CLEAR` ribbon cannot be printed via `SetSetting` on this
   SDK version; the mapper returns false for it and the orchestrator fails the
   job with `Evolis.RibbonNotSelectable` (500, a configuration problem, not a
   hardware one) rather than silently no-op'ing.
2. **Trays are not set via `SetSetting(GInputTray/GOutputTray, ...)`.** That
   key's own "Possible values" (`PRINTER, FEEDER, AUTO, MANUAL, HOPPER, BEZEL`
   for input; `PRINTER, HOPPER, REAR, MANUAL, REJECT, BEZEL` for output) is a
   different, narrower vocabulary than `Evolis.InputTray`/`OutputTray` (the
   enums `EvolisPrinterConfiguration` already uses) — confirmed live that
   `STANDARD`, this deployment's own configured default `OutputTray`, has no
   `GOutputTray` token at all (every attempt returned `false`). The plan's own
   step-8 pseudocode (`SetSetting(GOutputTray, config.OutputTray)`) is
   incompatible with this SDK version as literally written. The real,
   confirmed-working call is the typed `Connection.SetInputTray(Evolis.InputTray)`/
   `SetOutputTray(Evolis.OutputTray)`/`SetErrorTray(Evolis.OutputTray)` —
   round-tripped live (`SetInputTray(FEEDER) => True`, `SetOutputTray(STANDARD)
   => True`). `EvolisPrinterConnection.ApplyConfiguration` uses these, not
   `SetSetting`.
3. **`GDuplexType` is not the per-job single/double-sided toggle.** Its real
   vocabulary (`DUPLEX_CC, DUPLEX_CM, DUPLEX_MC, DUPLEX_MM`) describes which
   physical duplex module is installed — a one-time hardware fact with no
   `EvolisPrinterConfiguration` column at all, not something to set per job. The
   actual per-job face toggle is a separate key, `Duplex` (`NONE`/`HORIZONTAL`),
   confirmed from the SDK's own docs. `EvolisPrinterConnection.PrintAsync` sets
   `Duplex`, never touches `GDuplexType`.

### Real gap found and closed: how does the Agent get a printer's trays/coercivity at all?

The plan says trays come from `EvolisPrinterConfiguration`, "not the request,"
but never says how the Agent — no database, no session beyond the forwarded
Print Agent token — would fetch that out of band, and explicitly says Phase 5's
"Backend/API" is "None." Resolved the same way `RibbonSdkCode` already is:
`EvolisPrintReqDto` gained `InputTray`/`OutputTray`/`ErrorTray`/`MagCoercivity`
fields, caller-supplied — Angular already has them from `GET /api/printers/{id}`
(Phase 1) when the operator picks a printer, so it passes them straight
through. No new backend endpoint, matching the plan's own constraint.

### Design decisions made while building this, documented here since they deviate from the plan's literal text

- **`ICardPrinterAdapter.RunJobAsync` (Phase 3's stub) was removed, not filled
  in.** The orchestrator must call the Inventory API (resolve-for-print)
  *between* the magnetic read and the print step — impossible behind one opaque
  call without the adapter itself knowing about `IPrintFlowClient`, a layering
  violation. Replaced with `OpenAsync(CardJob) -> ICardPrinterConnection`, a new
  vendor-neutral session object (`ReadTrack2Async`, `GetLoadedRibbonSdkCodeAsync`,
  `PrintAsync`, `EjectAsync`, `RejectAsync`, `IAsyncDisposable`) the orchestrator
  drives step by step, with a real backend call free to happen in between. No
  `Evolis.*` type crosses this boundary either — confirmed by review of every
  method signature on it.
- **Execution order moves rendering before the ribbon check (plan step 7
  before step 6), and before the printer gate/ready-check/mag-read entirely.**
  The ribbon check's own rule reads `renderResult.Panel`, which can't exist
  before rendering runs - the plan's own step numbering is internally
  inconsistent on this point. Rendering is also a pure, hardware-free function
  (Phase 4), so running it first means an invalid cardholder name
  (`InvalidCardHolderNameException`) is rejected with zero side effects - no
  card pulled from the feeder, no resolve-for-print call spent - instead of
  discovering it after both have already happened.
- **Step 2's ready gate is coarser than the plan's pseudocode** (`WAR_FEEDER_EMPTY`,
  `INF_CARD_*`, etc.) because Phase 3 already established that
  `PrinterSnapshot` only carries a coarse `PrinterState` + `Capabilities.Magnetic`,
  not individual SDK flags — and that `GetStatus` genuinely fails for some real
  connection paths (TCP, confirmed in Phase 3). The gate rejects a *confirmed*
  bad state (`Error`/`Offline`/`Warning`) or a *confirmed* missing magnetic
  encoder, but lets `Unknown` state / null `Capabilities` proceed rather than
  block - rejecting those would make every TCP-linked printer in this
  deployment permanently unprintable, the opposite of the plan's own
  graceful-degradation rule (§13.4 #7).
- **Every early failure throws `EvolisException`, mirroring Matica's own
  catch-eject-rethrow shape** (reject the card first, then throw) rather than
  returning a result object - this orchestrator has no `IActionResult` to
  return directly the way `MachineController.Print` does inline, so
  `GlobalExceptionMiddleware` does the HTTP mapping instead, exactly as it
  already does for every other `MachineException`. Only the print/eject/
  record-result outcome (steps 8-11) returns as `CardJobResult`, preserving
  Matica's own 200/207 shape and its one quirk: an eject failure after a
  successful print still reports `CannotEject` even though Backend Call #2
  already ran with `success: true`.

### What was built

- `Params/EvolisPrintReqDto.cs` - request DTO, now including trays/coercivity (see gap above).
- `Enums/{InputTray,OutputTray,MagCoercivity}.cs` - created from scratch, mirroring the Inventory API's enums exactly.
- `Machine/Abstractions/{CardJob,ICardPrinterAdapter,IPrinterJobGate}.cs` - `CardJob`/`CardJobResult` filled in for real; `ICardPrinterAdapter` redesigned per the `RunJobAsync` removal above; new `ICardPrinterConnection`, `MagReadResult`, `CardPrintRequest`, `CardPrintOutcome` types.
- `Machine/Evolis/{EvolisPrinterConnection,EvolisPrintSettingsMapper,EvolisRibbonCompatibilityChecker,IPanExtractor,PanExtractor}.cs` - created. `EvolisPrinterAdapter.cs`/`EvolisConnectionScope.cs`/`EvolisErrors.cs` modified.
- `Machine/Orchestration/{IEvolisCardJobOrchestrator,EvolisCardJobOrchestrator,PrinterJobGate}.cs` - created; the twelve-step flow.
- `Controllers/MachineController.cs` - `PrintEvolis` action added; existing `Print`/Matica actions untouched.
- `Resources/Localization/Messages.resx`/`.ar.resx` - 5 new `Evolis.*` keys, EN + Egyptian Arabic.
- `Program.cs` - DI wiring for the new orchestrator, gate (singleton), settings mapper, ribbon checker, PAN extractor.

**Open item, not resolved this phase (flagged, not guessed past):** whether a
real encoded card's track 2 carries ISO 7813 sentinels (`;`...`=`/`?`) around
the PAN, or bare digits like Matica's own cards - every card available for a
live probe this phase came back `MAG_EBLANK` (no magnetic data at all), so this
was never actually observed. `PanExtractor` handles both shapes defensively
(strips sentinels if present, no-ops otherwise, matching Matica's own
`Substring(0, 16)` when they're absent) but needs re-verification against a
real encoded card before relying on it in production.

### Verified live against the real connected Evolis Primacy 2 and the real running Inventory API - not a fake adapter

- `dotnet build` on the Agent: 0 errors.
- Confirmed all three SDK findings above by direct `SetSetting`/`Connection.Set*Tray`
  round-trips against the real printer in throwaway probe projects (deleted after use).
- Provisioned real end-to-end test data: logged in as tenant `rcdb` and as the
  seeded system admin, created a real Evolis printer (id 9, `DeviceName: "Evolis
  Primacy 2"`, branch 3/tenant 3, ribbon `KWHITE` matching what's actually
  loaded), minted a real print-agent-token scoped to it.
- Ran the real Agent against the real Inventory API and called the real
  endpoint with a real cardholder name, real product, real branch:
  - First call: the real magnetic read genuinely ran (~46s, consistent with the
    configured 30s `MagReadTimeoutMs` plus overhead) and returned `MAG_EBLANK`
    (this deployment's available card still carries no magnetic data at all,
    same finding as Phase 3/5's earlier probes) - the orchestrator correctly
    rejected the card and the response came back as the expected
    `422`-equivalent `Evolis.BlankTrack` error, fully round-tripped through
    `GlobalExceptionMiddleware` and the resx localization.
  - Second test, two genuinely concurrent requests for the same `DeviceName`:
    one came back `Machine.NotReady` ("printer state: Warning" - a real,
    live-read printer state, confirming the ready gate reacts to real status
    changes) and the other came back `Evolis.PrinterBusy` - confirming
    `PrinterJobGate`'s per-device semaphore actually serializes concurrent
    requests under real load, and that the losing request fails before
    touching the connection (no extra card spent).
- **Not exercised live, and cannot be without a real encoded card:** resolve-for-print,
  the ribbon compatibility check's actual comparison, the print/eject path, and
  record-print-result. Every available card in this deployment is magnetically
  blank, so the orchestrator never got past step 4 in any live run. These paths
  are covered by the same reasoning the plan itself applies to Phase 5
  (physical outcomes need Phase 6 hardware), but the fact that steps 0-4,
  the busy gate, and the ready gate are now confirmed against a real printer
  (not a fake) is strictly more than the plan's own Phase 5 "no hardware
  required" bar asked for.
- Test printer (id 9) and the resulting local state were cleaned up: printer
  deleted via the real API afterward. Both the Agent and Inventory API
  processes were stopped cleanly at the end of this session (no leftover PID,
  unlike the Phase 3 housekeeping note).

## Governing constraints (from the developer, apply to everything below)

- Treat the repository as the source of truth. Do not recreate files from
  assumption — read the real current content first.
- Do not guess SDK values or behavior. If something can't be verified, say so
  explicitly and ask, rather than guessing.
- Do not hand-write an EF migration if `dotnet ef migrations add` is viable —
  only hand-edit a generated migration where there's a specific, confirmed reason
  (see Task 1 below — that reason already exists and is confirmed).
- Do not touch code unrelated to this phase.
- Before making any change, explain what you found and what you intend to change,
  and wait for go-ahead if the question is open (e.g. Task 2 below is explicitly
  blocked pending developer input — don't guess past it).
- After each significant change, verify it (build, and for schema changes, a real
  `dotnet ef database update` against a real database).
- Keep changes incremental and reviewable.

## Repo / environment

- Repo: `BelalMuhamed/inventory`, branch `main`, Onion Architecture
  (`DomainLayer` → `ApplicationLayer` → `InfrastructureLayer` →
  `InventoryManagmentAndInstanceIssuancePresentationLayer`).
- Current HEAD at time of investigation: `7ef274a900cd95c7ff1f1c313ca5f10f5d36a2ef`
  — `feat(printing): add Evolis printer registry (schema + API) — Phase 1`.
  **This commit already contains the full Phase 1 draft** (entity, enums, repo,
  DTOs, controller wiring, and a generated migration
  `20261001094229_EvolisPrinterConfiguration .cs` — note the trailing space before
  `.cs` in that filename, it's real, don't "fix" it incidentally).
- Working tree was clean, `main` up to date with origin, as of the investigation.
  **Re-run `git status` / `git log -1` first** — time has passed and the developer
  may have made changes locally since.
- Sibling repo `BelalMuhamed/Matice-inventory-service-` (the Print Agent / Windows
  Service, project `invetoryBackGroundServices`) has **no Evolis SDK reference at
  all** — confirmed via exhaustive grep across both repos. Not relevant to either
  task below except as background.

## What the cloud investigation could NOT do (why this handoff exists)

The cloud container has no `dotnet`/EF SDK installed, no way to install one
(outbound network is allowlisted to package registries/GitHub only — a direct
`dotnet-install.sh` fetch was blocked), and no database connectivity. Everything
below that needs `dotnet build`, `dotnet ef`, or a real database **must** happen
here, on the developer's machine, where the Evolis SDK, .NET tooling, and the real
dev database all already exist.

---

## Task 1 — `RibbonTypes.SdkCode` migration: already fixed by the developer — REVIEW ONLY

**The developer has already written the fix for this locally.** Do not redo it,
do not regenerate the migration, do not rewrite its `SdkCode` step again. Your
job here is to **review** what's on disk against the root cause below and flag
anything that doesn't hold up — not to re-implement it.

### What to check during review

1. Confirm `Up()` now does the `SdkCode` column in three ordered steps
   (nullable add → backfill → `NOT NULL` + unique index), not the original
   single non-nullable `AddColumn` + `CreateIndex` pair.
2. Confirm the backfill step uses **real** `RibbonTypes.Name` values (query the
   actual table to check, don't take the migration's own comments at face
   value) — not an invented or placeholder mapping.
3. Confirm anything that doesn't match a known `Name` gets an obviously-wrong
   placeholder (e.g. `UNMATCHED_<Id>`) rather than silently landing on an empty
   or duplicate string — and that none actually remain unmatched after running
   it.
4. Run `dotnet build`, then `dotnet ef database update` against the real dev
   database, and confirm it applies cleanly.
5. Confirm it **reverses cleanly** too (`dotnet ef database update
   <previous-migration-name>`) on a copy of the populated database.
6. Confirm nothing else in the migration (the `EvolisPrinterConfigurations`
   table, its FKs, its other indexes) was changed incidentally while fixing
   `SdkCode`.

If review turns up a problem, report it and propose a fix rather than silently
patching it — same "explain before changing" rule as everything else in this
file.

### Background — original root cause (for context only, not to be re-fixed)

`InfrastructureLayer/Migrations/20261001094229_EvolisPrinterConfiguration .cs`
does, in `Up()`, in this order:

```csharp
// line ~14
migrationBuilder.AddColumn<string>(
    name: "SdkCode",
    table: "RibbonTypes",
    type: "nvarchar(30)",
    maxLength: 30,
    nullable: false,
    defaultValue: "");

// line ~67
migrationBuilder.CreateIndex(
    name: "UX_RibbonTypes_SdkCode",
    table: "RibbonTypes",
    column: "SdkCode",
    unique: true);
```

This is a single-step non-nullable column add with an empty-string default,
immediately followed by a unique index on that same column, in the same `Up()`.

**Confirmed via full-repo inspection: `RibbonTypes` has no seed mechanism
anywhere in version control.** Checked:
- `InfrastructureLayer/Data/DbSeeder.cs` in full — seeds only the bootstrap
  `SystemAdmin`, nothing else.
- Every migration from `20260609092119_init.cs` through HEAD (62 commits,
  full history, not just the shallow Phase-1 commit) — the original
  `RibbonTypes` table-creation migration (`20260810170233_printing-module-
  domain-and-persistence.patch.cs`) creates the table empty; no `HasData()`,
  no `migrationBuilder.Sql()` insert, anywhere.
- No `.sql` files in the repo at all.

**Conclusion: whatever rows exist in the developer's real `RibbonTypes` table
were inserted manually, outside Git.** If there are 2+ rows, this migration's
`CreateIndex ... unique: true` will fail — every existing row defaults to
`SdkCode = ""` from the `AddColumn`, and a unique index can't have two empty
strings.

This root cause is why the three-step rewrite (nullable add → backfill → `NOT
NULL` + unique index) was necessary. **It's already done** — see "What to check
during review" above for exactly what to verify on disk, and don't redo the
rewrite yourself.

---

## Task 2 — Verify the real `EvolisTray` SDK values (BLOCKED — cannot be done from the repo)

### Current state (self-flagged placeholder, already shipped)

`DomainLayer/Enums/EvolisTray.cs`:
```csharp
public enum EvolisTray : byte
{
    FEEDER = 0,
    MANUAL = 1,
    STANDARD = 2,
    ERROR = 3,
    REAR = 4
}
```
Its own XML doc comment already says this is **not independently verified
against the SDK** — it's a placeholder covering the plan doc's stated defaults
plus tray-adjacent `SettingsKey` names seen in an earlier hardware probe
(`GInputTray`, `GOutputTray`, `GRejectBox`, `GFeederCfg`/`GFeederPos`), not a
confirmed SDK enum. It's wired into `EvolisPrinterConfiguration.InputTray` /
`OutputTray` (default `STANDARD`) / `ErrorTray` (default `ERROR`), persisted via
`HasConversion<byte>()` in `AppDbContext.cs`.

### Why this can't be resolved from either repo

Confirmed via exhaustive grep across both `inventory` and
`Matice-inventory-service-`: **the Evolis SDK NuGet package is not referenced in
any `.csproj` in either repo.** No DLL, no XML docs, no source. The SDK only
exists where it was actually installed — the Windows machine the Phase 0
hardware probe ran against (Evolis Primacy 2, `Evolis.SDK` 9.4.1 /
`Evolis.SDK.Native.Core`, native `evolis.dll`).

What's already known from that earlier hardware probe (do not re-derive these,
they're confirmed):
- `CardPos` **is** a real SDK enum (`INSERT, INSERTBACK, INSERTEJECT, EJECT,
  REJECT, CONTACT, CONTACTLESS, SCAN, MAG, PRINT`) — but it's about card
  *movement* operations, not tray *selection*. It is not the right mapping
  target for `EvolisTray`.
- `RibbonType`, `MagFormat`, `Model`, `SettingsKey` are **not** C# enums in this
  SDK — they're classes with public static constant fields, discovered via
  reflection (`Enum.GetNames` throws `ArgumentException` on them).
- `SettingsKey` includes `GInputTray`, `GOutputTray`, `GFeederCfg`,
  `GFeederPos`, `GRejectBox` among ~166 keys, consumed via
  `SetSetting(SettingsKey, string)` — i.e. **string-valued**, not a dedicated
  enum. The valid string vocabulary for these specific keys was never captured.

### What to do

1. On the developer's machine, where the SDK is actually installed: inspect the
   installed package for a dedicated tray/card-position enum type distinct from
   `CardPos`. Check:
   - `lib/netstandard2.0/evolis-sdk-cs.xml` (or wherever the installed NuGet
     package puts XML docs) for anything matching `Tray`, `CardPos`,
     `GInputTray`, `GOutputTray`, `GFeederCfg`, `GFeederPos`, `GRejectBox`.
   - The installed DLL's public types directly (e.g. `ilspycmd` against
     `evolis.dll`, or decompile via the IDE) to see every public type the SDK
     exposes — don't rely on docs alone if they're incomplete.
   - `dotnet nuget locals global-packages --list` to find where the Evolis
     NuGet package actually lives on disk if it's not referenced in this repo's
     `.csproj` yet (it may be referenced in a different project, or only
     installed, not yet committed — check before assuming it's missing
     entirely).
2. Report the exact type name(s), member names, and numeric values found —
   verbatim, not paraphrased. Show the evidence (file path + excerpt, or
   decompiled signature) before changing `EvolisTray.cs`.
3. **If no dedicated tray enum exists** and trays are genuinely only
   configurable as free-form strings via `SetSetting(SettingsKey, string)`,
   that's a design change, not a bug fix: `EvolisPrinterConfiguration.InputTray`
   / `OutputTray` / `ErrorTray` would need to become `string` columns (with a
   documented valid-value set, if the SDK enumerates one) instead of a
   `tinyint`-backed enum. **Bring this back as an explicit decision for the
   developer to confirm — do not silently resolve it either way**, since it
   changes the migration, the entity, the DTOs, and already-written service code
   (`PrinterConfigurationService.cs`).
4. Only once the real values (or the string-based redesign) are confirmed,
   update `EvolisTray.cs` (or the column types) accordingly, and regenerate/
   hand-adjust the migration as needed — following the same "don't hand-write
   if `dotnet ef` works" rule as Task 1.

---

## Order of operations

1. `git status` / `git log -1` — confirm nothing has changed locally since this
   brief was written; report if it has.
2. Confirm local tooling: `dotnet --version`, `dotnet ef --version`,
   `dotnet build` (clean, before any changes) — report results.
3. Task 1 — **review only**: walk the "What to check during review" checklist
   against the migration already on disk. Build and run `dotnet ef database
   update` to confirm it actually applies, and confirm it reverses cleanly too.
   Report what you found; flag anything that doesn't hold up rather than fixing
   it silently.
4. Task 2 — ask the developer for SDK evidence or inspect the installed SDK
   directly on this machine; report the exact findings before changing
   `EvolisTray.cs` or anything downstream of it.
5. Report back what was reviewed/found, what (if anything) needs a fix, and
   what's still open, before considering Phase 1 complete.

Task 2 is still an explicit "do not guess" item — don't start changing
`EvolisTray.cs` or its column type until real SDK evidence is in hand.

**Superseded — see the "Status as of 2026-10-04" section at the top of this file.**
Both tasks are done; `EvolisTray.cs` no longer exists.
