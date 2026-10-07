# Evolis Printer Integration — Implementation Plan

**Target printer:** Evolis Primacy 2 (LAN, magnetic encoder fitted, flip-over/duplex fitted)
**SDK:** `Evolis.SDK` 9.4.1 (+ `Evolis.SDK.Native.Core` 9.4.1)
**Repos in scope:** `InventoryManagmentAndInstanceIssuanceSolution` (Inventory API) and `Matice-inventory-service-` (Printer Agent)
**Out of scope:** Angular (contracts only — decision Q-30)

---

## 1. Current Architecture

### 1.1 Inventory API — `InventoryManagmentAndInstanceIssuanceSolution`

Strict Onion, all four projects on `net8.0`.

```
DomainLayer/                Result, Error, AuditableEntity, Entities, Enums
ApplicationLayer/           Contracts (IUnitOfWork, IGenericRepo<T,TKey>, named repos),
                            DTOs, Errors, Options, ServicesContracts
InfrastructureLayer/        Data/AppDbContext.cs (all Fluent API), Migrations,
                            Repositories, Security, Services, UnitOfWork.cs
InventoryManagmentAndInstanceIssuancePresentationLayer/
                            Controllers, Common (ApiResponse<T>, ResultExtensions),
                            Filters/LocalizeErrorResultFilter, Resources/Localization,
                            Program.cs
```

Relevant existing state:

| Element | Status |
|---|---|
| `DomainLayer/Enums/UsingPrinterType.cs` | `Evolis = 0`, `Matica = 1` — **already exists** |
| `DomainLayer/Entities/Printer.cs` | shared row: `Id, TenantId, BranchId, UsingPrinterType, Name, Model, UniqueNumber`, `Branch` nav |
| `DomainLayer/Entities/MaticaPrinterConfiguration.cs` | 1:1 extension, `Cascade` delete, `UX_MaticaPrinterConfigurations_PrinterId` |
| `DomainLayer/Entities/EvolisProductPrintConfiguration.cs` | **already exists and is complete** — `RibbonTypeId, PrintWay, X, Y, PrintedFace, FontFamily, FontSize, PrintColor, BackgroundColor, FontStyle, ImageId` |
| `DomainLayer/Entities/RibbonType.cs` | reference table, currently `Id` + `Name` only |
| `DomainLayer/Enums/PrintWay.cs` | `Landscape = 0`, `Portrait = 1` |
| `DomainLayer/Enums/PrintedFace.cs` | `Front = 0`, `Back = 1` |
| Print-flow endpoints | `resolve-for-print`, `record-print-result` — vendor-neutral, already live |
| Reconciliation service account | `POST /api/auth/service-token`, client-credentials, branch-scoped, revocable |

**Known contradiction to be removed in Phase 1.** `InfrastructureLayer/Data/AppDbContext.cs` (`ConfigurePrinterRegistry`) states verbatim: *"Evolis needs no extension table at all (module requirement §1)"*, and `CreatePrinterRequest` rejects a config payload when `UsingPrinterType == Evolis`. Decision Q-15 supersedes this. Requires a PM note against module requirement §1.

### 1.2 Printer Agent — `Matice-inventory-service-/invetoryBackGroundServices`

`net8.0`, ASP.NET Core hosted as a Windows Service (`AddWindowsService`), bound to `http://localhost:8403`.

```
Common/          ApiContracts.cs (ApiResponse<T>, ApiError), MachineErrors.cs,
                 ErrorResponseWriter.cs, MachineOperationResult.cs, PrintResult.cs
Controllers/     MachineController.cs, HardwareIoPocController.cs
Filters/         LocalizeErrorResultFilter.cs
Machine/         IMaticaCommandClient/MaticaCommandClient.cs, MachineHttpTransport.cs,
                 MachineExceptions.cs
Middleware/      GlobalExceptionMiddleware.cs
Options/         ServiceOptions.cs, InventoryApiOptions.cs, PrintAgentAuthOptions.cs
Outbox/          OutboxStore.cs (FileOutboxStore), OutboxReconciliationJob.cs
PrintFlow/       PrintFlowDtos.cs
Params/          PrintReqDto.cs, GetMachineInfoReques.cs
Resources/       Localization/Messages.resx, Messages.ar.resx, Messages.cs
Security/        PrintAgentAuthPolicy.cs, PrintAgentClaims.cs, FileEncryptionService.cs
Services/        IPrintFlowClient/PrintFlowClient.cs, ReconciliationTokenProvider.cs
cls/ lan/        ENCRYPTION.cs, GLOBALS.cs, UTILITIES.cs, Logger.cs, CardDataBean.cs
Program.cs
```

Existing Matica print flow, inline in `MachineController.Print`:

```
ready check (GetInfoAsync) → LoadCard → ReadMAG(track 2) → PAN = first 16 chars
  → Backend Call #1 resolve-for-print → Emboss → encrypted LocalBatch.lbt line
  → EjectCard → Backend Call #2 record-print-result (idempotency key, outbox on failure)
```

Reusable, vendor-neutral, **not modified by this plan**: `IPrintFlowClient`, `IOutboxStore`, `OutboxReconciliationJob`, `IReconciliationTokenProvider`, `PrintAgentAuthPolicy`, `PrintAgentClaims`, `ApiResponse<T>`, `MachineError`/`MachineErrorCategory`, `GlobalExceptionMiddleware`, `LocalizeErrorResultFilter`, `Logger`, `IFileEncryptionService`.

`Controllers/HardwareIoPocController.cs` — inspected. Its own doc comment says it is a temporary proof-of-concept with no `[Authorize]`, not wired into the print flow, to be deleted once validated. **Unrelated to this work. Not touched by any phase.**

### 1.3 Reference implementation — `Matica-Service` branch `belal`, folder `EvolisService`

Built on **ESPF** (JSON-RPC over named pipe `ESPFServer00`), not the SDK. Superseded by decision Q-13. Its only carried-forward assets are the bitmap geometry (1016×648) and the BMP format choice. Its defects are catalogued in §14.

### 1.4 SDK surface — verified from `lib/netstandard2.0/evolis-sdk-cs.xml`

| Member | Verbatim / verified |
|---|---|
| `Evolis.Evolis.GetDevices()` | *"Returns a list of Evolis devices installed on the system."* |
| `Device` | `id, name, displayName, uri, mark, model, isSupervised, isOnline, link, driverVersion` |
| `Device.isSupervised` | *"Tells if the printer is supervised or not by evolis premium suite service."* |
| `Device.link` | `USB` \| `TCP` \| `Unknown` |
| `Connection(Device\|string, OpenMode)` | `OpenMode` = `AUTO` \| `DIRECT` \| `SUPERVISED` |
| `Connection.Reserve()/Release()` | *"No one else can reserve the printer. No one else can start a printing job."* |
| `Connection.SetSessionManagement(bool)` | default true; *"In case of 45s of inactivity, the session is released."* |
| `Connection.GetStatus(out Status)` | `config / information / warning / error / exts / session`, 256 named flags |
| `Connection.GetEvent()` / `SetEvent(name, ServiceAction)` | `ServiceAction` = `OK` \| `RETRY` \| `CANCEL` |
| `Connection.SetErrorManagement(ErrorManagement)` | `PRINTER` \| `SOFTWARE` \| `SUPERVISED` (**SUPERVISED is read-only**) |
| `Connection.GetRibbonInfo(out RibbonInfo)` | ribbon identification |
| `AbstractConnection.SetCardPos / InsertCard / EjectCard / RejectCard` | `CardPos` = `INSERT, INSERTBACK, INSERTEJECT, EJECT, REJECT, CONTACT, CONTACTLESS, SCAN, MAG, PRINT` |
| `PrintSession(Connection, RibbonType)` | `Init()`, `SetImage(CardFace, byte[])`, `SetBlack(...)`, `SetOverlay(...)`, `SetSetting(SettingsKey,string)`, `SetAutoEject(bool)`, `Print(int timeoutMs)` → `ReturnCode` |
| `PrintSession.SetImage` | *"Set the bitmap image data to print using the YMC panels only."* |
| `MagSession(Connection)` | `Init()`, `SetCoercivity`, `SetFormat`, `SetTrack`, `Write(ms)`, `Read(ms)`, `ReadTracks(t1,t2,t3,ms)`, `GetData(MagTrackNumber)`, `GetStatus(track)` |
| `MagSession.ReadTracks` | *"Card is inserted from input tray if no card present in the printer."* |
| `SettingsKey` | 166 typed keys, incl. `GRibbonType, GDuplexType, Duplex, GInputTray, GOutputTray, GMagCoercivity, GMagT1/2/3Encoding, FPageRotate180, BPageRotate180` |
| `ReturnCode` | 70 values, incl. `OK, ENEEDACTION, ETIMEOUT, PRINT_NEEDACTION, PRINT_EMECHANICAL, PRINT_WAITCARDINSERT, PRINT_EUNKNOWNRIBBON, PRINT_ENOIMAGE, MAG_ERROR, MAG_EDATA, MAG_EBLANK, PRINTER_ENOCOM, PRINTER_EBUSY, SESSION_EBUSY, SESSION_ETIMEOUT` |
| `Evolis.Evolis.TIMEOUT` | 30 s printer communication timeout |
| `Evolis.Evolis.PRINT_TIMEOUT` | default print job timeout |
| Native packaging | `runtimes/win-x64/native/evolis.dll`, `runtimes/win-x86/native/evolis.dll` — RID-resolved, **no manual P/Invoke or DLL copying** |

Status flags that matter to this plan:

- **Capability:** `CFG_MAGNETIC`, `CFG_FLIP`, `CFG_SMART`, `CFG_CONTACTLESS`, `CFG_ETHERNET`, `CFG_WIFI`, `CFG_USB_OVER_IP`
- **Supervision:** `INF_PCSVC_LOCKED` (*"The EPS is supervising the printer"*), `INF_PRINTER_MASTER` (*"Error management is set to 'printer'"*)
- **Card position:** `INF_CARD_FEEDER, INF_CARD_PRINT, INF_CARD_MAG*, INF_CARD_FLIP, INF_CARD_EJECT, INF_CARD_HOPPER`
- **Errors:** `ERR_COVER_OPEN, ERR_MECHANICAL, ERR_FEEDER_ERROR, ERR_RIBBON_ERROR, ERR_RIBBON_ENDED, ERR_BAD_RIBBON, ERR_REJECT_BOX_FULL, ERR_HOPPER_FULL, ERR_HEAD_TEMP, ERR_NO_OPTION, ERR_BLANK_TRACK, ERR_MAGNETIC_DATA, ERR_READ_MAGNETIC, ERR_WRITE_MAGNETIC`
- **Warnings:** `WAR_FEEDER_EMPTY, WAR_COVER_OPEN, WAR_NO_RIBBON, WAR_RIBBON_ENDED, WAR_UNSUPPORTED_RIBBON, WAR_REJECT_BOX_FULL, WAR_HOPPER_FULL, WAR_COOLING, WAR_PRINTER_LOCKED, WAR_WAIT_CARD`

Ribbon tokens confirmed present in the SDK assembly: `YMCKO, YMCKOK, YMCKOO, YMCK, YMCKK, YMCKS, YMCKKS, YMCKOS, YMCKOKOS, YMCKOKS13, YMCKOS13, YMCKH, YMCKI, YMCKKI, KBLACK, KBLACK_R, KSCRATCH`. The authoritative full list is enumerated in Phase 0.

Card geometry, from the Evolis Consumables table: **CR-80, 300×300 dpi → 1016 × 648 px** (D2T2).

Transport constraint, from the Evolis connection guide: *"By default, no smart contact / contactless encoding option is compatible with Ethernet communication."* Magnetic over Ethernet is supported. **The LAN-only + magnetic-only deployment is valid; chip encoding would not be.**

Spooler constraint, from the Evolis Desktop-vs-Kiosk guide: for Primacy 2, `ExtEscape()` with `PASSTHROUGH` *"is still in development"*. **The Windows GDI/spooler print path must not be used for this model** — which is an independent confirmation of decision Q-13.

---

## 2. Confirmed Decisions

All decisions below are **closed**. They are not assumptions.

| # | Decision |
|---|---|
| Q-13 | Rebuild on `Evolis.SDK` 9.4.1; do not port the ESPF implementation |
| Q-14 | Print configuration is applied per printer/product type; no shared config shape across vendors |
| Q-15 | New `EvolisPrinterConfigurations` table, 1:1 with `Printer` |
| Q-16 | Cardholder name only — no additional print fields |
| Q-17 | Flow is Application → SDK → Driver → Printer |
| Q-19 | Add `RibbonTypes.SdkCode` |
| Q-20 | SkiaSharp for rendering, not `System.Drawing.Common` |
| Q-21 | Agent self-registers `SupervisorHostName` |
| Q-22 | API is source of truth for registered printers; Agent reports runtime state; Angular reconciles |
| Q-23 | Image-based printing (cardholder name may be in colour) |
| — | Image format: BMP |
| Q-24 | Magnetic is **read-only**; PAN is the first 16 characters of track 2, identical to Matica |
| Q-25 | Panel selection branches on `PrintColor`; if the loaded ribbon cannot satisfy it, fail with a clear localized error |
| Q-26 | **Separate endpoint** for Evolis; the Matica endpoint and DTO are untouched |
| Q-27 | **Add alongside**; no refactor of the existing Matica orchestration |
| Q-28 | Reuse the reconciliation service account, extended with a printer-registration scope |
| Q-29 | **Configuration-driven** `OpenMode` / `ErrorManagement`, defaulting to `DIRECT` + `SOFTWARE` |
| Q-30 | API contracts only; no Angular implementation |
| Q-31 | Code delivered inline per phase; no patch files |

Full rationale, alternatives, and impact for each: **§13**.

---

## 3. Remaining Questions

**None.** There are no blocking decisions outstanding.

Four items are **unknowns pending hardware**, not decisions. Phase 0 exists solely to resolve them, and every later phase is written to accept either outcome:

| Unknown | Resolved by | Fallback already designed |
|---|---|---|
| Does `SetErrorManagement(SOFTWARE)` succeed while EPS supervises? | Phase 0 step 4 | Q-29 config switch → set `SUPERVISED` + longer timeouts |
| Does `Reserve()` succeed while `INF_PCSVC_LOCKED` is set? | Phase 0 step 3 | `ReserveOnJob=false` config flag; rely on SDK session management alone |
| Does `SetImage`/`SetBlack` accept BMP bytes? | Phase 0 step 8 | `ICardRenderer` returns `byte[]`; swap encoder only |
| Does track 2 come back with ISO sentinels (`;` … `?`)? | Phase 0 step 7 | `IPanExtractor` strips sentinels before the 16-char take |

---

## 4. Overall Architecture

### 4.1 Runtime topology

```
                    ┌──────────────────────────────────────────┐
                    │  Branch supervising machine (one per      │
                    │  branch — hosts driver + EPS + Agent)     │
   Angular  ────────┤                                           │
      │             │   Printer Agent (Windows Service :8403)   │
      │             │        │                                  │
      │             │        ├── EvolisPrinterAdapter           │
      │             │        │        │                         │
      │             │        │        └── Evolis.SDK 9.4.1      │
      │             │        │                 │                │
      │             │        │                 └── evolis.dll   │
      │             │        │                         │        │
      │             │        └── MaticaCommandClient    │       │
      │             └──────────────────────────────────┼────────┘
      │                                                 │
      │                                    Evolis driver / EPS
      │                                                 │
      │                                        LAN (TCP :9100)
      │                                                 ▼
      │                                        Evolis Primacy 2
      ▼
 Inventory API  ◄──── resolve-for-print / record-print-result / supervisor self-registration
      │
      ▼
  SQL Server
```

### 4.2 Print job flow (Evolis)

```
Angular
   │  POST /api/Machine/Print-Card-Holder-Name-Evolis   (print-agent token)
   ▼
MachineController.PrintEvolis
   │
   ▼
EvolisCardJobOrchestrator                       ← vendor-neutral steps live here
   │   1. per-printer concurrency gate
   │   2. status gate            ─────────────┐
   │   3. reserve                             │
   │   4. read track 2 → PAN                  │  delegated to
   │   5. Backend Call #1  resolve-for-print  │  ICardPrinterAdapter
   │   6. ribbon compatibility check ─────────┤
   │   7. render bitmap (ICardRenderer)       │
   │   8. print                 ──────────────┤
   │   9. encrypted LocalBatch.lbt line       │
   │  10. eject / reject        ──────────────┤
   │  11. Backend Call #2  record-print-result│  (+ outbox on failure)
   │  12. release               ──────────────┘
   ▼
EvolisPrinterAdapter  →  Evolis.SDK  →  evolis.dll  →  driver/EPS  →  printer
```

### 4.3 Layer responsibility boundary

| Concern | Owner | Never touches |
|---|---|---|
| Auth, token scope, envelope | `MachineController` | hardware |
| Step order, backend calls, outbox, idempotency, local batch | `EvolisCardJobOrchestrator` | `Evolis.*` types |
| Connection, Reserve/Release, MagSession, PrintSession, settings, status→error | `EvolisPrinterAdapter` | HTTP, backend |
| Name → bitmap | `ICardRenderer` | printer, SDK |
| Protocol | `Evolis.SDK` | — |
| Transport | driver / EPS | — |

**Hard rule:** no `Evolis.*` type crosses out of `Machine/Evolis/`. The orchestrator sees only `ICardPrinterAdapter`, `CardJob`, `CardJobResult`, `PrinterSnapshot`, and `MachineException`.

### 4.4 Discovery and reconciliation

```
GET /api/printers?branchId=…       (Inventory API)  →  registered set   [source of truth]
GET /api/machine/evolis/printers   (Agent)          →  discovered set   [runtime truth]

join on DeviceName
```

| UI state | Registered? | Discovered here? | `isOnline` | Source |
|---|---|---|---|---|
| Supervised here | yes | yes | true | both |
| Offline | yes | yes | false | Agent `Device.isOnline` |
| Registered but not on this machine | yes | no | — | API only |
| Discovered but not registered | no | yes | — | Agent only |

### 4.5 Scoping model

```
Branch              = authorization scope   (token claim → controller check → API validation)
SupervisorHostName  = descriptive metadata  (display + runtime assertion only)
```

`SupervisorHostName` is deliberately **not** an authorization axis. Three reasons: branch scoping is already enforced end-to-end and a second axis would create a weaker parallel path to the same decision; host is knowable authoritatively only at runtime on that host, so a persisted value is advisory by nature; and one-machine-per-branch makes the two 1:1 today, so a second axis would buy nothing while doubling the failure modes. If a future requirement genuinely needs per-machine authorization, it is added as a claim on the print-agent token — not by promoting this column.

### 4.6 Extensibility

```csharp
services.AddKeyedScoped<ICardPrinterAdapter, EvolisPrinterAdapter>(UsingPrinterType.Evolis);
// Matica joins later, when its inline orchestration is migrated (deferred by Q-27):
// services.AddKeyedScoped<ICardPrinterAdapter, MaticaPrinterAdapter>(UsingPrinterType.Matica);
```

A third vendor is: one `UsingPrinterType` member, one adapter, one options class, one error map, one resx block, one DI line. `EvolisCardJobOrchestrator` generalises to `CardJobOrchestrator` at that point. Unchanged in all cases: outbox, token flow, `IPrintFlowClient`, both backend calls, `ApiResponse<T>`, localization filter.

**Acknowledged limitation of Q-27.** Until Matica migrates behind `ICardPrinterAdapter`, two orchestration paths coexist — the inline one in `MachineController.Print` and the new `EvolisCardJobOrchestrator`. Shared concerns (idempotency key generation, outbox write, local batch format) are therefore duplicated in two places and must be kept in sync by hand. This is the accepted cost of leaving the hardware-validated Matica path untouched. A future Phase 7 (out of scope here) would collapse them.

---

## 5. Phase 0 — Hardware Capability Probe

> Throwaway console application. Not shipped. Its **only** deliverable is a filled-in answer sheet that Phases 3–6 consume.

### Objective
Resolve the four hardware unknowns in §3 and capture the authoritative SDK enum values, before any production code is written against assumptions.

### Scope
A single `net8.0` console project, run on the branch supervising machine with the Primacy 2 powered on and connected.

### Database / Backend / Angular
None.

### Agent changes
None (separate scratch project).

### Files
```
Evolis.Probe/                                            CREATE  (scratch solution, not committed)
├── Evolis.Probe.csproj                                  CREATE  net8.0, x64, PackageReference Evolis.SDK 9.4.1
└── Program.cs                                           CREATE  the probe script below
```

### Probe script — what to run, in order

```
 1. Evolis.GetVersion()                       → record SDK + native version
 2. Evolis.SetLogLevel(TRACE); SetLogPath(...) → capture a full native trace for the whole run
 3. Evolis.GetDevices()                       → for each device print:
                                                  id, name, displayName, uri, mark, model,
                                                  isSupervised, isOnline, link, driverVersion
 4. new Connection(device, OpenMode.DIRECT)   → did it open while EPS is running?
 5. conn.Reserve()                            → true, or SESSION_EBUSY?
 6. conn.SetErrorManagement(SOFTWARE)         → accepted or refused? then GetErrorManagement()
 7. conn.GetStatus(out s)                     → print every set flag by name; specifically:
                                                  INF_PCSVC_LOCKED, INF_PRINTER_MASTER,
                                                  CFG_MAGNETIC, CFG_FLIP, CFG_ETHERNET
 8. conn.GetInfo(out info); conn.GetRibbonInfo(out r)  → model + loaded ribbon
 9. new MagSession(conn).ReadTracks(false,true,false, 30000); GetData(TRACK2)
                                              → EXACT raw string, character by character:
                                                sentinels present? length? PAN at [0..16)?
10. render a 1016x648 BMP, PrintSession.SetBlack(FRONT, bmp) → Print(60000)  → ReturnCode
11. same with SetImage(FRONT, bmp)            → ReturnCode  (expect failure on a mono ribbon)
12. empty the feeder, repeat step 10          → does Print() return PRINT_NEEDACTION, or block?
                                                if it returns, call GetEvent() and print it,
                                                then SetEvent(name, ServiceAction.CANCEL)
13. reflection dump — the authoritative enum lists:
        Enum.GetNames(typeof(Evolis.RibbonType))
        Enum.GetNames(typeof(Evolis.MagFormat))
        Enum.GetNames(typeof(Evolis.Model))
        Enum.GetNames(typeof(Evolis.SettingsKey))
14. conn.Release(); conn.Close()
```

### Tests
Manual execution only. **Requires real Evolis hardware.**

### Acceptance criteria
A written answer sheet containing:

- [ ] `OpenMode` that successfully opens a connection, and whether `SUPERVISED` is forced
- [ ] Whether `SetErrorManagement(SOFTWARE)` is accepted; the value `GetErrorManagement` reports afterwards
- [ ] Whether `Reserve()` succeeds; if not, the exact `ReturnCode`
- [ ] The exact raw track-2 string, with sentinel characters marked
- [ ] Whether BMP is accepted by `SetBlack` and by `SetImage`
- [ ] The ribbon token reported by the loaded ribbon
- [ ] `PRINT_NEEDACTION` vs blocking behaviour on an empty feeder, and the `GetEvent()` payload
- [ ] Full `RibbonType`, `MagFormat`, `Model`, `SettingsKey` name lists
- [ ] `CFG_MAGNETIC` and `CFG_FLIP` confirmed set

### Expected result
All four §3 unknowns closed; Phase 1's `RibbonTypes` seed list is exact rather than partial.

### Dependencies
None. **This is the first phase.**

### Decisions required before starting
None.

---

## 6. Phase 1 — Inventory API: Schema and Printer Registry

### Objective
Persist everything the Agent needs to address and configure an Evolis printer, and give `RibbonTypes` an SDK-facing value.

### Architecture
Pure Domain + Infrastructure + Presentation work in the Inventory API. No Agent code. Establishes the source of truth that Phase 3's self-registration writes into and Phase 5's print flow reads from.

### Database

**New table — `EvolisPrinterConfigurations`.** Mirrors `MaticaPrinterConfigurations` exactly: same aggregate as its `Printer`, `Cascade` delete, filtered unique index, soft-delete query filter.

Purpose: `Printer` holds what every vendor has (name, model, serial, branch). Everything below is meaningless for Matica and mandatory for Evolis, so it cannot live on the shared row without nullable columns that are wrong half the time. `DeviceName` in particular is the **SDK connection key** — the string `Evolis.GetDevices()` returns as `Device.name` and that `new Connection(name, mode)` consumes. Without it the Agent cannot address the printer at all.

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `bigint` IDENTITY | no | PK |
| `TenantId` | `bigint` | no | FK → `Tenants.Id`, `NoAction`; denormalized per codebase convention |
| `PrinterId` | `bigint` | no | FK → `Printers.Id`, **`Cascade`**, unique |
| `DeviceName` | `nvarchar(200)` | no | matches `Device.name`; the SDK connection key |
| `SupervisorHostName` | `nvarchar(100)` | **yes** | self-registered by the Agent (Q-21); null until first discovery |
| `SupervisorRegisteredAt` | `datetime2` | yes | staleness indicator for the UI |
| `DefaultRibbonTypeId` | `bigint` | yes | FK → `RibbonTypes.Id`, `NoAction`; printer-level default |
| `MagCoercivity` | `tinyint` | no | `PRINTER=0, AUTO=1, LOCO=2, HICO=3`; default `0` |
| `InputTray` | `tinyint` | no | default `FEEDER` |
| `OutputTray` | `tinyint` | no | default `STANDARD` |
| `ErrorTray` | `tinyint` | no | default `ERROR` |
| `OpenMode` | `tinyint` | no | Q-29; `AUTO=0, DIRECT=1, SUPERVISED=2`; default `DIRECT` |
| `ErrorManagement` | `tinyint` | no | Q-29; `PRINTER=0, SOFTWARE=1`; default `SOFTWARE` |
| + `AuditableEntity` | | | `CreatedAt, UpdatedAt, IsDeleted, DeletedAt, DeletedBy` |

Indexes and constraints:
```
UX_EvolisPrinterConfigurations_PrinterId          UNIQUE (PrinterId)
IX_EvolisPrinterConfigurations_TenantId_DeviceName        (TenantId, DeviceName)
UX_EvolisPrinterConfigurations_TenantId_SupervisorHostName_DeviceName
        UNIQUE (TenantId, SupervisorHostName, DeviceName) WHERE IsDeleted = 0
                                                   AND SupervisorHostName IS NOT NULL
HasQueryFilter(e => !e.IsDeleted)
```

The third index is the real integrity rule: one physical device name may legitimately repeat across different machines, but must not be registered twice against the same machine.

**Deliberately NOT stored** — all runtime-discoverable, all guaranteed to go stale: `IsSupervised`, `IsOnline`, `Link`, `Uri`, `Model`, `DriverVersion`, loaded ribbon.

**Modified table — `RibbonTypes`.** Add:

| Column | Type | Null | Notes |
|---|---|---|---|
| `SdkCode` | `nvarchar(30)` | no | exact `Evolis.RibbonType` token |

```
UX_RibbonTypes_SdkCode   UNIQUE (SdkCode) WHERE IsDeleted = 0
```

Why both columns exist: `Name` is **operator-facing** and will be renamed — someone will change "YMCKO" to "شريط ملون" or "Colour ribbon" in an admin screen, and if `Name` were also the SDK token that rename would silently break every print job. `SdkCode` is **machine-facing**, unique, and must never be edited by an operator. This is the same separation as `Printer.Name` vs `Printer.UniqueNumber`.

**Seed data.** Use the authoritative list captured in Phase 0 step 13. Partial list verified from the SDK assembly:

| Name (editable) | SdkCode (fixed) |
|---|---|
| Colour — YMCKO | `YMCKO` |
| Colour duplex — YMCKOK | `YMCKOK` |
| Colour — YMCKOO | `YMCKOO` |
| Colour — YMCK | `YMCK` |
| Colour — YMCKK | `YMCKK` |
| Half-panel colour — YMCKOS | `YMCKOS` |
| Monochrome black | `KBLACK` |
| Monochrome black (rewrite) | `KBLACK_R` |
| Scratch-off | `KSCRATCH` |

**Data migration concern.** `RibbonTypes` may already hold rows referenced by live `EvolisProductPrintConfigurations`. `SdkCode` is `NOT NULL` + unique, so the migration must be three ordered steps in one migration file: add the column nullable → `UPDATE` each existing row with its token (matching on `Name`; any row that cannot be matched gets a deliberate placeholder and is reported) → `ALTER COLUMN ... NOT NULL` and create the unique index. **Never** add the column non-nullable with a constant default — that would violate the unique index on the second row.

### Backend / API

Endpoints: no new routes in this phase. Existing printer endpoints extend their payloads.

```
POST   /api/printers            → accepts EvolisConfig when UsingPrinterType == Evolis
PUT    /api/printers/{id}       → same
GET    /api/printers            → PrinterResponse now carries EvolisConfig
GET    /api/printers/{id}       → same
GET    /api/ribbon-types        → RibbonTypeResponse gains SdkCode
```

Validation rules (mirror the existing Matica rules, inverted):
- `UsingPrinterType == Evolis` → `EvolisConfig` **required**, `MaticaConfig` must be absent
- `UsingPrinterType == Matica` → `MaticaConfig` required, `EvolisConfig` must be absent
- `DeviceName` required, ≤ 200, trimmed, non-whitespace
- `DefaultRibbonTypeId`, when supplied, must exist
- `SupervisorHostName` is **not accepted** from this endpoint — it is written only by Phase 2's self-registration route

`PrinterConfigurationService` must insert printer + Evolis config in one `ExecuteInTransactionAsync` / one `SaveChanges`, setting the `Printer` **navigation** (not `PrinterId`) so EF fixes up the FK from the generated identity — the exact mechanism `MaticaPrinterConfiguration.Printer` already relies on.

Restore/self-heal path: follow `IMaticaPrinterConfigRepo.GetByPrinterIdIncludingDeletedAsync`. `PrinterId` is uniquely constrained unconditionally, so a soft-deleted row still occupies the slot; restoring a printer must find and revive that row rather than insert a duplicate.

### Agent changes
None.

### Angular
Contract only: `PrinterResponse.evolisConfig` appears; `RibbonTypeResponse.sdkCode` appears.

### Files

```
Inventory API
├── DomainLayer/
│   ├── Entities/EvolisPrinterConfiguration.cs                      CREATE
│   ├── Entities/RibbonType.cs                                      MODIFY  + SdkCode
│   └── Enums/
│       ├── MagCoercivity.cs                                        CREATE  byte enum
│       ├── EvolisTray.cs                                           CREATE  byte enum (input/output/error)
│       ├── EvolisOpenMode.cs                                       CREATE  byte enum
│       └── EvolisErrorManagement.cs                                CREATE  byte enum
├── ApplicationLayer/
│   ├── Contracts/IEvolisPrinterConfigRepo.cs                       CREATE  mirror IMaticaPrinterConfigRepo
│   ├── Contracts/IUnitOfWork.cs                                    MODIFY  + EvolisPrinterConfigs
│   ├── DTOs/Printing/PrintingDtos.cs                               MODIFY  + EvolisPrinterConfigRequest/Response,
│   │                                                                        RibbonTypeResponse + SdkCode,
│   │                                                                        CreatePrinterRequest/UpdatePrinterRequest,
│   │                                                                        PrinterResponse
│   └── Errors/PrintingErrors.cs                                    MODIFY  + EvolisConfigRequired,
│                                                                             EvolisConfigNotAllowed,
│                                                                             DeviceNameRequired,
│                                                                             DuplicateDeviceNameOnHost
├── InfrastructureLayer/
│   ├── Data/AppDbContext.cs                                        MODIFY  DbSet + ConfigurePrinterRegistry;
│   │                                                                        DELETE the "Evolis needs no extension
│   │                                                                        table" doc claim; RibbonType.SdkCode
│   ├── Data/DbSeeder.cs                                            MODIFY  ribbon seed with SdkCode
│   ├── Repositories/EvolisPrinterConfigRepo.cs                     CREATE
│   ├── UnitOfWork.cs                                               MODIFY  expose the new repo
│   ├── Services/PrinterConfigurationService.cs                     MODIFY  create/update/restore Evolis branch
│   └── Migrations/<ts>_evolis-printer-configuration.cs             CREATE  (dotnet ef migrations add)
└── PresentationLayer/
    ├── Controllers/PrintersController.cs                           MODIFY  payload wiring only
    ├── Swagger/Examples/PrintingExamples.cs                        MODIFY  Evolis examples
    └── Resources/Localization/Messages.resx + Messages.ar.resx     MODIFY  new error keys, EN + Egyptian Arabic
```
**Requires repository inspection:** exact file name of the printer Swagger example class and of `DbSeeder`'s ribbon seed method.

### Deletions
None. One doc-comment claim in `AppDbContext.ConfigurePrinterRegistry` is removed (not a file).

### Tests
**No hardware required.**

- Unit: `PrinterConfigurationService` — Evolis create; Evolis update; Evolis + `MaticaConfig` supplied → rejected; Evolis with no config → rejected; restore of a soft-deleted printer reuses the existing config row
- Unit: validators — missing/blank/over-length `DeviceName`; unknown `DefaultRibbonTypeId`; `SupervisorHostName` supplied by a client → ignored or rejected
- Integration: migration applies to a database containing pre-existing `RibbonTypes` rows without dropping them; `SdkCode` ends up `NOT NULL` and unique
- Integration: two printers with the same `DeviceName` under **different** `SupervisorHostName` → allowed; under the **same** host → rejected
- Integration: soft-deleting a printer soft-deletes its Evolis config; `UX_..._PrinterId` still holds

### Acceptance criteria
- [ ] Migration applies cleanly forward and reverses cleanly on a populated database
- [ ] `RibbonTypes.SdkCode` is `NOT NULL`, unique among non-deleted rows, seeded from the Phase 0 list
- [ ] An Evolis printer can be created, read, updated, soft-deleted and restored with its config via the API
- [ ] `POST /api/printers` rejects a Matica config on an Evolis printer, and vice versa
- [ ] `PrinterResponse` returns `EvolisConfig` for Evolis printers and `null` for Matica
- [ ] All new error codes resolve in EN and Egyptian Arabic
- [ ] No behavioural change to any Matica endpoint (regression suite green)

### Dependencies
Phase 0 (for the exact ribbon seed list). The schema itself can be written in parallel; only the seed values block.

### Decisions required before starting
None.

---

## 7. Phase 2 — Inventory API: Supervisor Self-Registration

### Objective
Let the Agent write `SupervisorHostName` for the printers it actually discovers, under a scoped machine credential (Q-21, Q-28).

### Architecture
```
Printer Agent (startup + on-demand discovery)
   │  POST /api/printers/supervisor-registration   Bearer <service-account token>
   ▼
PrintersController.RegisterSupervisor
   │
   ▼
PrinterConfigurationService.RegisterSupervisorAsync
   │
   ▼
EvolisPrinterConfigurations.SupervisorHostName / SupervisorRegisteredAt
```

### Database
No new tables. Writes only the two columns added in Phase 1.

### Backend / API

```
POST /api/printers/supervisor-registration
Authorization: Bearer <service-account access token>

{
  "branchId": 12,
  "hostName": "BR12-PRINT01",
  "devices": [
    { "deviceName": "Evolis Primacy 2", "model": "Primacy 2",
      "link": "TCP", "driverVersion": "1.2.3.4", "isSupervised": true }
  ]
}
```

Response — the reconciliation result, which is what makes this endpoint useful rather than fire-and-forget:

```json
{ "success": true,
  "data": {
    "matched":   [ { "printerId": 44, "deviceName": "Evolis Primacy 2", "updated": true } ],
    "unmatched": [ { "deviceName": "Evolis Zenius 2", "reason": "NotRegistered" } ],
    "registeredButNotDiscovered": [ { "printerId": 45, "deviceName": "Evolis Primacy 2 B" } ]
  } }
```

Semantics:
- Match on `(TenantId, BranchId, DeviceName)` among non-deleted Evolis printers
- Match → set `SupervisorHostName = hostName`, `SupervisorRegisteredAt = UtcNow`
- No match → **do not auto-create a printer.** Report it as `unmatched`. Printer registration stays an explicit admin action; a device appearing on the network must never silently become a registered asset.
- A printer already bearing a *different* `SupervisorHostName` → overwrite, and log the change at warning level. The last machine to see the device is the one supervising it.
- **Idempotent.** Re-posting identical content changes nothing but `SupervisorRegisteredAt`.

Authorization (Q-28): extend the existing reconciliation service account rather than minting a fourth key.

- Add scope `printer:register-supervisor` to the service-account model
- `POST /api/auth/service-token` issues it when the account holds it
- New policy `PrinterSupervisorRegistrationPolicy` requires that scope **and** a `branchId` claim matching the request body
- The account remains branch-scoped and revocable exactly as it is today

Rationale for reusing it: the credential already has the right scope shape (machine-to-machine, branch-bound, revocable, no user session) and already runs on the same host for outbox reconciliation. A fourth signing key would add key-rotation surface for one additional write.

### Agent changes
None in this phase — the Agent side of the call is built in Phase 3.

### Angular
Contract only: the printer list may display `supervisorHostName` and `supervisorRegisteredAt`.

### Files

```
Inventory API
├── ApplicationLayer/
│   ├── DTOs/Printing/PrintingDtos.cs                  MODIFY  + SupervisorRegistrationRequest,
│   │                                                            DiscoveredDeviceDto,
│   │                                                            SupervisorRegistrationResponse
│   ├── ServicesContracts/IPrinterConfigurationService.cs  MODIFY  + RegisterSupervisorAsync
│   └── Errors/PrintingErrors.cs                       MODIFY  + SupervisorBranchMismatch,
│                                                                 SupervisorHostNameRequired
├── InfrastructureLayer/
│   ├── Services/PrinterConfigurationService.cs        MODIFY  + RegisterSupervisorAsync
│   ├── Repositories/EvolisPrinterConfigRepo.cs        MODIFY  + GetByBranchAndDeviceNamesAsync (bulk, 1 query)
│   └── Security/ServiceAccount*.cs                    MODIFY  + scope constant  ← Requires repository inspection
└── PresentationLayer/
    ├── Controllers/PrintersController.cs              MODIFY  + RegisterSupervisor action
    ├── Security/AuthorizationPolicies.cs              MODIFY  + PrinterSupervisorRegistrationPolicy
    ├── Program.cs                                     MODIFY  register the policy
    └── Resources/Localization/Messages*.resx          MODIFY  new keys
```
**Requires repository inspection:** the service-account entity/scope representation and the `service-token` issuing service.

### Tests
**No hardware required.**

- Unit: match / unmatched / registeredButNotDiscovered partitioning
- Unit: host overwrite when a different machine reports the same device
- Unit: an unregistered device is reported, never inserted
- Integration: token without `printer:register-supervisor` → 403
- Integration: token whose `branchId` ≠ body `branchId` → 403
- Integration: posting the same payload twice is idempotent
- Integration: bulk lookup issues exactly one query for N devices (no N+1)

### Acceptance criteria
- [ ] The endpoint exists, is reachable only with the new scope, and is branch-scoped
- [ ] `SupervisorHostName` / `SupervisorRegisteredAt` populate correctly
- [ ] Unregistered devices are reported and never auto-created
- [ ] Repeated calls are idempotent
- [ ] Existing reconciliation (outbox) behaviour is unaffected by the scope addition

### Dependencies
Phase 1 (columns must exist).

### Decisions required before starting
None.

---

## 8. Phase 3 — Agent: SDK Plumbing, Discovery, Self-Registration

### Objective
Bring `Evolis.SDK` into the Agent, expose discovery as a device-proxy endpoint, push discovery results to the API, and build the connection/status/error foundation the print flow will sit on. **No printing yet.**

### Architecture
```
Angular ──► GET /api/machine/evolis/printers ──► EvolisDeviceDiscovery ──► Evolis.GetDevices()
                                                        │
Agent startup / on demand ──────────────────────────────┘
                     │
                     └──► POST /api/printers/supervisor-registration  (Phase 2)
```

### Database
None.

### Backend / API
None (consumes Phase 2).

### Agent changes

**Packages** — `invetoryBackGroundServices.csproj`:
```xml
<PackageReference Include="Evolis.SDK" Version="9.4.1" />
<PlatformTarget>x64</PlatformTarget>
```
`Evolis.SDK.Native.Core` arrives transitively and RID-resolves `runtimes/win-x64/native/evolis.dll`. Pin `x64` so the native probe is unambiguous inside a service host.

**Options** — `EvolisOptions`, section `Evolis`:

| Setting | Default | Purpose |
|---|---|---|
| `OpenMode` | `DIRECT` | Q-29 |
| `ErrorManagement` | `SOFTWARE` | Q-29 |
| `ReserveOnJob` | `true` | fallback switch if Phase 0 step 5 returns `SESSION_EBUSY` |
| `CommandTimeoutMs` | `30000` | matches `Evolis.TIMEOUT` |
| `MagReadTimeoutMs` | `30000` | |
| `PrintTimeoutMs` | `120000` | generous; Primacy 2 YMCKO duplex is slow |
| `AutoRegisterSupervisorOnStartup` | `true` | Q-21 |
| `NativeLogPath` | `""` | when set, `Evolis.SetLogPath` |
| `NativeLogLevel` | `Warning` | |

**Discovery endpoint** (device proxy — Q-22):

```
GET /api/machine/evolis/printers        [Authorize(PrintAgentAuthPolicy)]

{ "success": true,
  "data": {
    "hostName": "BR12-PRINT01",
    "devices": [{
      "deviceName": "Evolis Primacy 2", "displayName": "Evolis Primacy 2",
      "model": "Primacy 2", "uri": "…", "link": "TCP",
      "isSupervised": true, "isOnline": true, "driverVersion": "1.2.3.4",
      "capabilities": { "magnetic": true, "flip": true, "ethernet": true,
                        "smart": false, "contactless": false },
      "supervisedByEps": true,
      "ribbon": { "sdkCode": "YMCKO" },
      "state": "Ready"
    }]
  } }
```

Field provenance — exactly as required by §9 of the brief:

| Response field | Source |
|---|---|
| `hostName` | `Environment.MachineName` |
| `deviceName`, `displayName`, `model`, `uri`, `link`, `isOnline`, `isSupervised`, `driverVersion` | `Device` from `Evolis.GetDevices()` |
| `capabilities.*` | `Status.config` flags: `CFG_MAGNETIC`, `CFG_FLIP`, `CFG_ETHERNET`, `CFG_SMART`, `CFG_CONTACTLESS` |
| `supervisedByEps` | `Status.information` flag `INF_PCSVC_LOCKED` |
| `ribbon.sdkCode` | `Connection.GetRibbonInfo()` |
| `state` | derived by `EvolisStatusMapper` from `Status.error` / `.warning` / `.information` |

`isSupervised` and `supervisedByEps` are **two independent signals** and are both returned: the first comes from the driver/EPS layer without opening a connection, the second from the device itself. Disagreement is diagnostic information, not an error — surface both.

Capability, ribbon and state require opening a connection, so discovery does so **per device, briefly, read-only** — open, `GetStatus`, `GetRibbonInfo`, close. No `Reserve()`, because reserving blocks the LCD and would disrupt an operator mid-job.

**Connection lifecycle.** Open-per-operation, not a long-lived pool:

```csharp
using var conn = _factory.Open(deviceName);   // IDisposable → Release() if reserved, then Close()
```

Reasons: the SDK auto-releases a session after 45 s of inactivity, so a long-lived connection would silently lose its session between jobs and need re-acquisition anyway; a pooled connection held across jobs would keep the LCD locked and block walk-up operators; and a crashed job must not leak a reservation. Open cost is small relative to a multi-second print.

**Status → error mapping.** `EvolisStatusMapper` translates flags and `ReturnCode` into the existing `MachineError` shape. Full table in §10. `EvolisException : MachineException` slots into the existing `GlobalExceptionMiddleware` unchanged.

**Self-registration.** `EvolisSupervisorRegistrar` — an `IHostedService` that runs discovery at startup (when `AutoRegisterSupervisorOnStartup`), obtains a token from `IReconciliationTokenProvider`, and POSTs to Phase 2's endpoint. Failure is logged and **never fatal** — the Agent must still print if the API is briefly unreachable. Also invoked after any manual discovery call.

### Angular
Contract only: the discovery response above, to be joined against `GET /api/printers` per §4.4.

### Files

```
Agent — invetoryBackGroundServices/
├── invetoryBackGroundServices.csproj                       MODIFY  + Evolis.SDK, PlatformTarget x64
├── Options/EvolisOptions.cs                                CREATE
├── Machine/Abstractions/
│   ├── ICardPrinterAdapter.cs                              CREATE  Discover / GetSnapshot / RunJob
│   ├── PrinterSnapshot.cs                                  CREATE  vendor-neutral status projection
│   ├── CardJob.cs                                          CREATE  (shape only; used in Phase 5)
│   └── CardJobResult.cs                                    CREATE
├── Machine/Evolis/
│   ├── EvolisConnectionFactory.cs                          CREATE  Open(deviceName) → EvolisConnectionScope
│   ├── EvolisConnectionScope.cs                            CREATE  IDisposable; applies OpenMode,
│   │                                                                ErrorManagement, optional Reserve
│   ├── EvolisDeviceDiscovery.cs                            CREATE  GetDevices() + per-device probe
│   ├── EvolisStatusMapper.cs                               CREATE  flags/ReturnCode → MachineError, state
│   ├── EvolisErrors.cs                                     CREATE  error factories (mirrors MachineErrors)
│   ├── EvolisExceptions.cs                                 CREATE  EvolisException : MachineException
│   └── EvolisPrinterAdapter.cs                             CREATE  discovery + status only in this phase
├── Services/
│   ├── IEvolisSupervisorRegistrationClient.cs              CREATE
│   ├── EvolisSupervisorRegistrationClient.cs               CREATE
│   └── EvolisSupervisorRegistrar.cs                        CREATE  IHostedService
├── Params/EvolisDiscoveryDtos.cs                           CREATE  response DTOs above
├── Controllers/EvolisMachineController.cs                  CREATE  route api/machine/evolis
├── Resources/Localization/Messages.resx                    MODIFY  Evolis error keys (EN)
├── Resources/Localization/Messages.ar.resx                 MODIFY  Evolis error keys (Egyptian Arabic)
└── Program.cs                                              MODIFY  Configure<EvolisOptions>,
                                                                     AddKeyedScoped<ICardPrinterAdapter,
                                                                       EvolisPrinterAdapter>(UsingPrinterType.Evolis),
                                                                     AddHttpClient<IEvolisSupervisorRegistrationClient…>,
                                                                     AddHostedService<EvolisSupervisorRegistrar>,
                                                                     Evolis.SetLogPath/SetLogLevel at startup
```

A `UsingPrinterType` enum must exist Agent-side for the keyed registration. **Requires repository inspection:** whether the Agent already mirrors this enum or whether it must be added under `Enums/`.

### Deletions
None.

### Tests

**No hardware required:**
- Unit: `EvolisStatusMapper` — every documented error/warning flag maps to the intended `MachineError`; unknown flag combinations degrade to a generic internal error rather than throwing
- Unit: `ReturnCode` → `MachineError` for all 70 values, with an exhaustive-switch guard
- Unit: `EvolisSupervisorRegistrationClient` — payload shape, token attached, non-2xx does not throw
- Unit: `EvolisConnectionScope` disposal calls `Release` only when it reserved
- Integration: `EvolisDeviceDiscovery` behind an interface seam returns an empty list without throwing when no device is present
- Integration: the Agent starts and serves requests with **no printer attached and no driver installed** — this is the critical negative test

**Requires real Evolis hardware:**
- Discovery returns the device with correct `link`, `isSupervised`, `isOnline`
- `capabilities.magnetic` and `capabilities.flip` are `true`
- Self-registration populates `SupervisorHostName` in the database

### Acceptance criteria
- [ ] Agent builds and runs as a Windows Service with the SDK referenced, x64
- [ ] `GET /api/machine/evolis/printers` returns the attached Primacy 2 with all fields populated
- [ ] The same endpoint returns an empty list, HTTP 200, on a machine with no Evolis printer
- [ ] `SupervisorHostName` appears in the database after startup
- [ ] An unreachable Inventory API at startup does not prevent the Agent from serving requests
- [ ] Native SDK log file is produced when `NativeLogPath` is configured
- [ ] Every Evolis error code resolves in EN and Egyptian Arabic
- [ ] No change in behaviour for any existing Matica endpoint

### Dependencies
Phase 0 (`OpenMode` / `ErrorManagement` / `Reserve` answers), Phase 2 (registration endpoint).

### Decisions required before starting
None.

---

## 9. Phase 4 — Agent: Card Rendering with SkiaSharp

### Objective
Turn a cardholder name plus an `EvolisProductPrintConfiguration` into a CR-80 BMP, and decide which ribbon panel that bitmap belongs on (Q-25).

### Architecture
```
Cardholder Name  +  EvolisPrintConfig
        │
        ▼
   ICardRenderer  ──► SkiaCardRenderer
        │
        ▼
  CardRenderResult { byte[] BmpBytes; CardPanel Panel; int Width; int Height }
        │
        ▼
 EvolisPrinterAdapter ──► PrintSession.SetBlack(...)  or  SetImage(...)
```

Self-contained and fully testable **without hardware or a printer** — a pure function from inputs to bytes.

### Database
None.

### Backend / API
None.

### Agent changes

**Package:** `SkiaSharp` + `SkiaSharp.NativeAssets.Win32`.

Why SkiaSharp and not `System.Drawing.Common` (Q-20): Microsoft's guidance is that `System.Drawing` types are not supported inside a Windows service or ASP.NET, and the Agent is both; GDI handle exhaustion is a known long-running-process failure; and `System.Drawing.Common` is Windows-only from .NET 7. SkiaSharp is MIT (ImageSharp's split licence would require a commercial licence for Modupay), has no GDI dependency, and gives deterministic text metrics.

**Geometry:**

| `PrintWay` | Canvas |
|---|---|
| `Landscape` (0) | 1016 × 648 |
| `Portrait` (1) | 648 × 1016 |

**Config → render mapping:**

| Field | Effect |
|---|---|
| `PrintWay` | canvas orientation (above) |
| `BackgroundColor` | `canvas.Clear(SKColor.Parse(hex))` — already validated as hex by the API |
| `PrintColor` | `SKPaint.Color`, and the panel decision below |
| `FontFamily` | `SKTypeface.FromFamilyName(...)`; fall back to a configured default and **log a warning** if the family is absent on the host |
| `FontStyle` | `SKFontStyle` — Regular / Bold / Italic / BoldItalic |
| `FontSize` | points → pixels at 300 dpi: `px = pt * 300 / 72` |
| `X`, `Y` | text origin in pixels, top-left, clamped to the canvas with a warning if out of bounds |
| `PrintedFace` | `CardFace.FRONT` / `CardFace.BACK`; `BACK` implies duplex — see Phase 5 |

**Panel decision (Q-25 — the decisive logic):**

```
isMonochromeBlack = PrintColor parses to a colour whose R,G,B are all <= threshold (default 32)

isMonochromeBlack → CardPanel.ResinBlack  → PrintSession.SetBlack(face, bmp)
otherwise         → CardPanel.Colour      → PrintSession.SetImage(face, bmp)
```

`SetImage` is documented as *"using the YMC panels only"*, so a colour name is physically impossible on a `KBLACK` ribbon. That check is **not** performed here — the renderer has no printer — but `CardRenderResult.Panel` is precisely the input Phase 5's ribbon compatibility check consumes.

For `ResinBlack`, output 1-bit-per-pixel black/white; for `Colour`, 24-bpp RGB. Both encoded as **BMP** (decision: BMP, pending Phase 0 step 8).

`BackgroundColor` on a resin panel: a non-white background would mean printing a solid black field across the whole card. The renderer treats background as white for `ResinBlack` and logs once if the configured background is not white. **Flag to the operator via the API, not silently.**

### Angular
None.

### Files

```
Agent — invetoryBackGroundServices/
├── invetoryBackGroundServices.csproj                    MODIFY  + SkiaSharp, SkiaSharp.NativeAssets.Win32
├── Machine/Rendering/
│   ├── ICardRenderer.cs                                 CREATE
│   ├── CardRenderRequest.cs                             CREATE  name + flattened Evolis config
│   ├── CardRenderResult.cs                              CREATE  BmpBytes, Panel, Width, Height, Warnings
│   ├── CardPanel.cs                                     CREATE  enum { ResinBlack, Colour }
│   ├── SkiaCardRenderer.cs                              CREATE
│   └── CardGeometry.cs                                  CREATE  constants 1016/648, dpi 300, pt→px
├── Options/EvolisOptions.cs                             MODIFY  + DefaultFontFamily, MonochromeThreshold
└── Program.cs                                           MODIFY  AddSingleton<ICardRenderer, SkiaCardRenderer>
```

### Deletions
None. The reference `CardBitmap.cs` and its `System.Drawing` helpers are **not** carried over — they are superseded, and they carry a `using System.Windows;` (WPF) that would not compile here.

### Tests
**All without hardware. This is the most testable phase — test it hard.**

- Unit: landscape → 1016×648; portrait → 648×1016
- Unit: output bytes begin with the BMP signature `0x42 0x4D`; header dimensions match
- Unit: `PrintColor = #000000` → `ResinBlack`; `#FF0000` → `Colour`; `#1A1A1A` → `ResinBlack` at the default threshold
- Unit: point→pixel conversion — 12 pt at 300 dpi = 50 px
- Unit: missing font family → default substituted, warning emitted, no throw
- Unit: X/Y beyond the canvas → clamped, warning emitted
- Unit: non-white background with a resin panel → warning emitted, background forced white
- Unit: Arabic cardholder name renders without exception and produces non-blank pixels (RTL shaping sanity)
- Unit: very long name does not throw; behaviour is defined (clip or shrink — pick one and assert it)
- Golden-file: a fixed input produces a byte-identical BMP across runs (guards accidental renderer drift)
- Property: 1-bpp output contains only two distinct colours

**Requires hardware (deferred to Phase 6):** that a rendered BMP actually prints legibly and lands at the expected physical position.

### Acceptance criteria
- [ ] A cardholder name + config produces a valid BMP at the correct geometry
- [ ] Panel selection follows `PrintColor` per Q-25
- [ ] No `System.Drawing` reference remains anywhere in the Agent
- [ ] Arabic and Latin names both render
- [ ] Golden-file test passes
- [ ] Renderer is exercised entirely by unit tests, no printer involved

### Dependencies
None strictly — can be built in parallel with Phase 3. Phase 5 requires both.

### Decisions required before starting
None.

---

## 10. Phase 5 — Agent: Evolis Print Flow

### Objective
The end-to-end job: read the card, resolve it with the backend, print the name, record the result. Matica remains untouched (Q-26, Q-27).

### Architecture
```
Angular
  │  POST /api/Machine/Print-Card-Holder-Name-Evolis     [PrintAgentAuthPolicy]
  ▼
MachineController.PrintEvolis            ← new action on the EXISTING controller
  │
  ▼
EvolisCardJobOrchestrator
  │
  ├─ 1  per-printer SemaphoreSlim (by DeviceName)
  ├─ 2  adapter.GetSnapshotAsync        → ready gate
  ├─ 3  adapter connection open + Reserve
  ├─ 4  adapter.ReadTrack2Async         → MagSession.ReadTracks(false,true,false)
  ├─ 5  PAN = first 16 chars (after sentinel strip)  →  IPrintFlowClient.ResolveForPrintAsync
  ├─ 6  ribbon compatibility check      (Q-25)
  ├─ 7  ICardRenderer.Render            → BMP + panel
  ├─ 8  adapter.PrintAsync              → PrintSession → Print(timeout)
  ├─ 9  encrypted LocalBatch.lbt line
  ├─ 10 eject (success) / reject (failure)
  ├─ 11 IPrintFlowClient.RecordPrintResultAsync  (+ IOutboxStore on failure)
  └─ 12 Release + Dispose  (finally)
```

### Database
None.

### Backend / API
None — reuses `resolve-for-print` and `record-print-result` unchanged. **This is the point of the whole design: the backend contract is vendor-neutral and did not have to move.**

### Agent changes

**New request DTO** (`EvolisPrintReqDto`) — Evolis-shaped, `PrintReqDto` untouched:

```csharp
public sealed class EvolisPrintReqDto
{
    public required string CardHolderName { get; set; }
    public required string DeviceName { get; set; }   // SDK connection key
    public required long   ProductId { get; set; }
    public required long   BranchId  { get; set; }

    // from GET /api/products/{id}/print-config, same pattern as Matica's Font/Cpi/Offsets
    public required long   RibbonTypeId { get; set; }
    public required string RibbonSdkCode { get; set; }
    public required PrintWay    PrintWay { get; set; }
    public required PrintedFace PrintedFace { get; set; }
    public required int    X { get; set; }
    public required int    Y { get; set; }
    public required string FontFamily { get; set; }
    public required int    FontSize { get; set; }
    public required string PrintColor { get; set; }
    public required string BackgroundColor { get; set; }
    public required string FontStyle { get; set; }
}
```

No `MachineIp` / `Port` / `FeederId` / `HopperId` / `Font` / `Cpi` / `OffsetX` / `OffsetY` / tipper fields — none apply. Trays come from `EvolisPrinterConfiguration`, not the request.

**Step details.**

*Step 2 — ready gate.* The Evolis analogue of Matica's `machineStatus == READY && CardInside == no && TipperStatus == Ready`:

```
reject if   any Status.error flag is set
reject if   WAR_FEEDER_EMPTY | WAR_COVER_OPEN | WAR_NO_RIBBON | WAR_RIBBON_ENDED
          | WAR_UNSUPPORTED_RIBBON | WAR_REJECT_BOX_FULL | WAR_HOPPER_FULL
          | WAR_PRINTER_LOCKED
reject if   any INF_CARD_* flag is set        (a card is already inside)
reject if   CFG_MAGNETIC is not set           (the flow needs the encoder)
```

*Step 4 — magnetic read (Q-24, read-only).* `ReadTracks(track1:false, track2:true, track3:false, MagReadTimeoutMs)` then `GetData(TRACK2)`. Per its own documentation it inserts a card from the feeder when none is present, so **no separate LoadCard step is required** — a real difference from Matica's flow.

`MAG_EBLANK` → "card has no magnetic data"; `MAG_EDATA` → "magnetic data unreadable". Both reject the card and fail the job before Backend Call #1.

*Step 5 — PAN.* `IPanExtractor` strips ISO sentinels if present (Phase 0 step 9 says whether they are) then takes the first 16 characters, matching Matica exactly. As in the Matica flow, the raw PAN is **used only for Backend Call #1** — never logged, never persisted, never assigned elsewhere. Only `MaskedPan` from the response is used afterwards.

*Step 6 — ribbon compatibility (Q-25).* Three-way check before anything is printed:

```
loaded   = Connection.GetRibbonInfo()            → actual ribbon in the printer
expected = dto.RibbonSdkCode                     → product configuration
needed   = renderResult.Panel                    → ResinBlack | Colour

loaded ≠ expected                        → EvolisErrors.RibbonMismatch(expected, loaded)
needed = Colour  AND loaded has no YMC   → EvolisErrors.RibbonCannotPrintColour(loaded)
```

Both produce a **clear, localized, actionable** message naming the loaded ribbon and the required one — this is exactly what Q-25 asked for. The job stops here, before a card is wasted.

*Step 8 — print.*

```
PrintSession(conn, RibbonType.Parse(dto.RibbonSdkCode))
  Init()
  SetSetting(GRibbonType,  sdkCode)
  SetSetting(GInputTray,   config.InputTray)
  SetSetting(GOutputTray,  config.OutputTray)
  SetSetting(GDuplexType,  PrintedFace == Back ? duplex value : "NONE")
  SetAutoEject(false)                          ← orchestrator controls eject/reject
  panel == ResinBlack ? SetBlack(face, bmp) : SetImage(face, bmp)
  Print(PrintTimeoutMs)  → ReturnCode
```

`SetAutoEject(false)` is deliberate: on failure the card must go to the **reject** tray, not the output hopper, so a spoiled card is never handed to a customer.

*Step 10 — disposition.* Success → `EjectCard()`. Failure → `RejectCard()`. Eject occurs **before** Backend Call #2, matching the existing Matica reasoning: the physical card is already printed-or-spoiled, so it should not sit inside the machine during a retry loop.

*Steps 9/11 — unchanged from Matica.* Same `LocalBatch.lbt` line format via `ENCRYPTION.Enc_TripleDES`, same `Guid.NewGuid()` idempotency key generated once per physical attempt and reused on every retry, same `IOutboxStore` write when `RecordPrintResultAsync` fails transiently. **Duplicated by hand from `MachineController.Print` — see the Q-27 limitation in §4.6.**

**Concurrency.** `SemaphoreSlim(1,1)` per `DeviceName`, held in a `ConcurrentDictionary`, for the duration of the job.

*Why not `Channel<CardJob>` + a single background consumer per printer?* Because the SDK does not require it and the API contract does not want it. The endpoint is request/response, exactly like the existing Matica endpoint; a channel would turn it into submit-then-poll, forcing a job-status endpoint, job persistence across restarts, and an Angular change — none of which are in scope. The SDK already enforces single-use at the device via session management and `Reserve()`; the semaphore exists only to fail fast with a clear "printer busy" rather than surfacing a raw `SESSION_EBUSY`. `CancellationToken` flows from the HTTP request, so an abandoned request releases the gate naturally. If fire-and-forget batches are ever required, the orchestrator is already the correct seam and only its invocation changes.

### Angular
Contract only: the new endpoint and DTO above.

### Files

```
Agent — invetoryBackGroundServices/
├── Params/EvolisPrintReqDto.cs                          CREATE
├── Machine/Abstractions/
│   ├── ICardPrinterAdapter.cs                           MODIFY  + ReadTrack2Async, PrintAsync,
│   │                                                              EjectAsync, RejectAsync
│   ├── CardJob.cs / CardJobResult.cs                    MODIFY  fill in from Phase 3 stubs
│   └── IPrinterJobGate.cs                               CREATE  per-device semaphore abstraction
├── Machine/Evolis/
│   ├── EvolisPrinterAdapter.cs                          MODIFY  mag read, print, eject, reject
│   ├── EvolisPrintSettingsMapper.cs                     CREATE  config → SettingsKey
│   ├── EvolisRibbonCompatibilityChecker.cs              CREATE  Q-25 three-way check
│   ├── IPanExtractor.cs / PanExtractor.cs               CREATE  sentinel strip + 16-char take
│   └── EvolisErrors.cs                                  MODIFY  + RibbonMismatch,
│                                                                  RibbonCannotPrintColour,
│                                                                  MagBlank, MagUnreadable,
│                                                                  PrinterBusy, NotReady(detail)
├── Machine/Orchestration/
│   ├── IEvolisCardJobOrchestrator.cs                    CREATE
│   ├── EvolisCardJobOrchestrator.cs                     CREATE  steps 1–12
│   └── PrinterJobGate.cs                                CREATE  ConcurrentDictionary<string, SemaphoreSlim>
├── Controllers/MachineController.cs                     MODIFY  + PrintEvolis action ONLY.
│                                                                  Existing Print action, DTO, and all
│                                                                  Matica actions unchanged.
├── Resources/Localization/Messages.resx / .ar.resx      MODIFY  new keys, EN + Egyptian Arabic
└── Program.cs                                           MODIFY  register orchestrator, gate (singleton),
                                                                  PanExtractor, settings mapper
```

**Requires repository inspection:** whether `PrintWay` / `PrintedFace` enums already exist Agent-side or must be mirrored under `Enums/`.

### Deletions
None.

### Tests

**No hardware required** — the adapter sits behind `ICardPrinterAdapter`, so the whole orchestrator is testable with a fake:

- Unit: happy path calls the twelve steps in order
- Unit: not-ready → fails before any card is inserted; no backend call made
- Unit: `MAG_EBLANK` → card rejected, job fails, **Backend Call #1 never issued**
- Unit: `resolve-for-print` business failure → card rejected, no print attempted
- Unit: `resolve-for-print` transient failure → card rejected, error reported as `BackendUnavailable`
- Unit: ribbon mismatch → fails **after** resolve, before print; card rejected; message names both ribbons
- Unit: colour name on a mono ribbon → `RibbonCannotPrintColour`
- Unit: print failure → local batch written with failure status, card **rejected not ejected**, Backend Call #2 still issued
- Unit: `RecordPrintResultAsync` transient failure → outbox entry written with the same idempotency key
- Unit: idempotency key generated exactly once per attempt and reused across retries
- Unit: concurrent requests for the same `DeviceName` → second gets `PrinterBusy`; different devices run in parallel
- Unit: cancelled request releases the gate
- Unit: `Release` + `Dispose` run even when the job throws
- Unit: raw PAN never appears in any log sink (assert against a capturing logger)
- Integration: Matica regression suite unchanged and green

**Requires real Evolis hardware (Phase 6):** all physical outcomes.

### Acceptance criteria
- [ ] `POST /api/Machine/Print-Card-Holder-Name-Evolis` exists, `[Authorize]`d, branch-scope checked
- [ ] Full flow passes against a fake adapter, all twelve steps, all failure branches
- [ ] Matica endpoints are byte-identical in behaviour
- [ ] Raw PAN never logged or persisted
- [ ] Concurrent same-printer requests serialize; different printers do not block each other
- [ ] Every new error code resolves in EN and Egyptian Arabic
- [ ] Reservation is always released

### Dependencies
Phase 1 (config), Phase 3 (adapter, connection, errors), Phase 4 (renderer). Phase 0 for PAN sentinel handling.

### Decisions required before starting
None.

---

## 11. Phase 6 — Hardware Validation and Hardening

### Objective
Run the real flow on the real printer, finalize the Q-29 configuration defaults from observed behaviour, and implement the error-event loop that only hardware can exercise.

### Architecture
No new components. Behavioural completion of Phase 5 plus `GetEvent`/`SetEvent` handling.

### Database
Possibly a data-only update to `EvolisPrinterConfigurations.OpenMode` / `.ErrorManagement` defaults if Phase 0/6 show `DIRECT`+`SOFTWARE` is not viable. **No schema change** — that is precisely what decision Q-29 bought.

### Backend / API
None.

### Agent changes

**Error-event loop.** When `Print()` returns `ENEEDACTION` or `PRINT_NEEDACTION`:

```
name = conn.GetEvent()
classify(name):
    operator-recoverable (feeder empty, cover open, reject box full)
            → conn.SetEvent(name, ServiceAction.CANCEL)
              fail the job with a specific localized error naming the condition
    transient (cooling, wait-card)
            → conn.SetEvent(name, ServiceAction.RETRY), bounded retries (EvolisOptions.MaxEventRetries, default 1)
    unknown → conn.SetEvent(name, ServiceAction.CANCEL), generic error, log the raw event name at warning
```

The Agent **never** auto-retries a mechanical error and never leaves an event unanswered — an unanswered event holds the printer.

**Hardening items:**
- Confirm `PrintTimeoutMs` against measured duplex YMCKO time; raise if within 2× of observed
- `ClearMechanicalErrors()` exposed as an admin endpoint, not called automatically
- Confirm `SetAutoEject(false)` + explicit eject/reject behaves as intended
- Verify `INF_CARD_*` flags clear between jobs (no phantom "card inside")
- 50-card soak: no handle leak, no session leak, no memory growth
- Confirm the 45-second session timeout never fires mid-job at real print durations

### Files

```
Agent — invetoryBackGroundServices/
├── Machine/Evolis/EvolisEventHandler.cs                 CREATE  GetEvent/SetEvent classification
├── Machine/Evolis/EvolisPrinterAdapter.cs               MODIFY  wire the event loop into PrintAsync
├── Options/EvolisOptions.cs                             MODIFY  + MaxEventRetries; tuned timeouts
├── Controllers/EvolisMachineController.cs               MODIFY  + clear-mechanical-errors admin action
└── Resources/Localization/Messages*.resx                MODIFY  per-event operator messages
```

### Tests

**No hardware:** event classification is a pure function — table-driven unit tests over known event names; unknown name → CANCEL; retry budget respected.

**Requires real Evolis hardware — the full matrix:**

| Scenario | Expected |
|---|---|
| Normal print, mono ribbon, black name | card ejected, name legible at configured X/Y |
| Normal print, colour ribbon, coloured name | card ejected, colour correct |
| Duplex / `PrintedFace = Back` | prints on the back face |
| Portrait orientation | 648×1016 renders correctly oriented |
| Feeder empty at job start | ready gate rejects, no card consumed |
| Feeder empties mid-job | `PRINT_NEEDACTION` → specific error, no hang |
| Cover opened mid-job | specific error, printer recovers after closing |
| Ribbon removed | specific error naming the ribbon condition |
| Wrong ribbon loaded vs product config | `RibbonMismatch` **before** any card is consumed |
| Colour name on mono ribbon | `RibbonCannotPrintColour` before any card is consumed |
| Blank / unencoded card | `MagBlank`, card rejected, no backend call |
| Reject box full | specific error |
| Network cable pulled mid-job | `PRINTER_ENOCOM`, session released, next job succeeds |
| Inventory API down at Backend Call #2 | outbox entry written, reconciliation completes it later |
| Two concurrent requests, same printer | second gets `PrinterBusy` |
| Agent restarted mid-job | no stuck reservation on next start |
| 50-card soak | no leaks, no degradation |

### Acceptance criteria
- [ ] All scenarios above produce the documented behaviour
- [ ] Final `OpenMode` / `ErrorManagement` values recorded as deployment defaults, with the evidence
- [ ] No unanswered event ever holds the printer
- [ ] 50-card soak passes clean
- [ ] Measured print duration documented; `PrintTimeoutMs` set to at least 2× observed
- [ ] Every failure path produces a localized message an operator can act on without reading logs

### Dependencies
Phases 0–5, and physical access to a Primacy 2 with magnetic encoder and a stock of encoded cards.

### Decisions required before starting
None — but Phase 0's answer sheet must be in hand.

---

## 12. Cross-Phase Architecture

### 12.1 Product configuration → SDK mapping

Two distinct configuration levels, and conflating them is the most common design error here:

| Level | Stored in | Answers | Examples |
|---|---|---|---|
| **Printer-level** | `EvolisPrinterConfigurations` (Phase 1) | how this *machine* is wired and calibrated | `DeviceName`, trays, `MagCoercivity`, `OpenMode`, `ErrorManagement`, `DefaultRibbonTypeId` |
| **Product-level** | `EvolisProductPrintConfigurations` (already exists) | how this *card design* looks | ribbon, orientation, face, font, colours, position |

The same product prints identically on any Evolis printer; the same printer prints every product with its own trays and coercivity. Neither level can absorb the other.

**Complete product-level mapping:**

| Column | Consumed by | Becomes |
|---|---|---|
| `RibbonTypeId` → `RibbonTypes.SdkCode` | adapter | `PrintSession(conn, RibbonType.X)` + `SetSetting(GRibbonType, sdkCode)`; also the left side of the Q-25 compatibility check |
| `PrintedFace` | adapter | `CardFace.FRONT` / `CardFace.BACK`; `BACK` also drives `SetSetting(GDuplexType, …)` because `SetImage(BACK, …)` implicitly sets duplex |
| `PrintWay` | renderer | canvas orientation — 1016×648 vs 648×1016. **Not** an SDK setting; `FPageRotate180`/`BPageRotate180` are 180° flips, not portrait, and are not used |
| `FontSize` | renderer | `pt × 300 / 72` → pixels |
| `PrintColor` | renderer **and** adapter | `SKPaint.Color`; and selects `SetBlack` vs `SetImage` (Q-25) |
| `BackgroundColor` | renderer | `canvas.Clear(...)`; forced white on a resin panel, with a warning |
| `FontFamily` | renderer | `SKTypeface.FromFamilyName` + fallback |
| `FontStyle` | renderer | `SKFontStyle` |
| `X`, `Y` | renderer | text origin in pixels |
| `ImageId` | — | **not used in this scope.** Q-16 is cardholder name only. The column stays; nothing reads it. |

**Printer-level mapping:**

| Column | Becomes |
|---|---|
| `DeviceName` | `new Connection(deviceName, openMode)` |
| `InputTray` / `OutputTray` | `SetSetting(GInputTray / GOutputTray, …)` |
| `ErrorTray` | `SetErrorTray(...)` |
| `MagCoercivity` | `MagSession.SetCoercivity(...)` — set even for read-only use, because it governs how the head interprets the stripe |
| `OpenMode` / `ErrorManagement` | `EvolisConnectionScope` (Q-29) |
| `DefaultRibbonTypeId` | fallback when a product has no ribbon configured; never overrides one that does |
| `SupervisorHostName` | display + runtime assertion only — **never** an authorization input |

### 12.2 What each phase leaves behind

| After | The system can |
|---|---|
| Phase 0 | nothing new — but every later assumption is evidence-backed |
| Phase 1 | register an Evolis printer with its device name and machine settings |
| Phase 2 | learn which machine supervises which printer |
| Phase 3 | list and inspect the printers attached to this machine |
| Phase 4 | turn a name into a printable bitmap |
| Phase 5 | print a card end-to-end, with the backend in the loop |
| Phase 6 | survive real-world failure |

---

## 13. Error Handling Strategy

### 13.1 Boundaries

| Boundary | Raised by | Surfaces as |
|---|---|---|
| **Validation** | DTO/model binding | `MachineErrorCategory.Validation`, 422 |
| **Business** | `resolve-for-print` rejection | `BackendRejected`, 4xx |
| **API transport** | Inventory API unreachable/5xx | `BackendUnavailable` → outbox |
| **Agent** | orchestrator bugs, gate contention | `Internal` / `PrinterBusy` |
| **SDK** | `ReturnCode` ≠ `OK` | `EvolisException` → mapped |
| **Driver / EPS** | `PRINTER_ENOCOM`, `SVC_*` | `CommunicationFailure` |
| **Printer status** | `Status.error` / `.warning` flags | `MachineRejected` with a specific message |
| **Network** | `PRINTER_NETWORK_*` | `CommunicationFailure` |

All of them arrive at the caller through the **existing** `ApiResponse<T>` / `ApiError` envelope and are localized by the **existing** `LocalizeErrorResultFilter` using the error code as the resx key. No new error plumbing is introduced.

### 13.2 `ReturnCode` classification

| ReturnCode | Category | Retryable | Audience |
|---|---|---|---|
| `OK` | — | — | — |
| `ETIMEOUT`, `SESSION_ETIMEOUT` | Timeout | **yes**, once | user |
| `SESSION_EBUSY`, `PRINTER_EBUSY` | MachineRejected | **yes**, after gate wait | user ("printer busy") |
| `PRINTER_ENOCOM`, `PRINTER_NETWORK_ERROR` | CommunicationFailure | **yes**, once | user |
| `PRINTER_NETWORK_ECERT` | CommunicationFailure | no | admin |
| `ENEEDACTION`, `PRINT_NEEDACTION` | MachineRejected | via event loop only | user, specific |
| `PRINT_EMECHANICAL` | MachineRejected | **no** | **printer intervention** |
| `PRINT_EUNKNOWNRIBBON` | MachineRejected | no | user — load a known ribbon |
| `PRINT_ENOIMAGE`, `PRINT_EDATA`, `PRINT_WSETTING` | Internal | no | logged; a bug in our code |
| `MAG_EBLANK` | MachineRejected | no | user — unencoded card |
| `MAG_EDATA`, `MAG_ERROR` | MachineRejected | no | user — re-feed the card |
| `EPARAMS`, `EUNSUPPORTED` | Internal | no | logged; a bug |
| `EDISABLED`, `SESSION_DISABLED` | Internal | no | admin — configuration |
| `USER_E*`, `SYSTEM_EUNAUTHORIZED_PRINTER` | Forbidden | no | admin |
| `ECANCELLED` | — | no | expected on cancellation |
| everything else | Internal | no | logged with the raw code |

`EvolisStatusMapper` uses an **exhaustive switch**; any unmapped value produces `Internal` and logs the raw name, so a future SDK version never crashes the Agent.

### 13.3 Status flags → operator messages

| Flag | Message intent | Retryable |
|---|---|---|
| `WAR_FEEDER_EMPTY` / `ERR_FEEDER_ERROR` | refill the feeder | after refill |
| `WAR_COVER_OPEN` / `ERR_COVER_OPEN` | close the cover | after closing |
| `WAR_NO_RIBBON` / `WAR_RIBBON_ENDED` / `ERR_RIBBON_ENDED` | replace the ribbon | after replacing |
| `WAR_UNSUPPORTED_RIBBON` / `ERR_BAD_RIBBON` | unsupported ribbon fitted | no |
| `WAR_REJECT_BOX_FULL` / `ERR_REJECT_BOX_FULL` | empty the reject box | after emptying |
| `WAR_HOPPER_FULL` / `ERR_HOPPER_FULL` | empty the output hopper | after emptying |
| `ERR_MECHANICAL` | **service intervention** | no |
| `ERR_HEAD_TEMP` / `WAR_COOLING` | cooling; wait | yes, after delay |
| `ERR_READ_MAGNETIC` / `ERR_BLANK_TRACK` | card unreadable / unencoded | no |
| `ERR_NO_OPTION` | requested option not fitted | no — configuration error |
| `WAR_PRINTER_LOCKED` | unlock the printer | after unlocking |

### 13.4 Non-negotiable rules

1. A failed card is **rejected**, never ejected to the customer hopper.
2. Backend Call #2 runs on **every** attempt, success or failure — the backend's record must match physical reality.
3. The idempotency key is generated **once per physical attempt** and reused on every retry.
4. The raw PAN exists only between step 4 and step 5. Never logged, never persisted, never returned.
5. `Release()` and `Dispose()` run in `finally`, unconditionally.
6. No event is ever left unanswered.
7. An unmapped SDK code degrades to `Internal` + log. It never throws an unhandled exception.

---

## 14. Concurrency Strategy

**Model:** one `SemaphoreSlim(1,1)` per `DeviceName`, held in a singleton `ConcurrentDictionary<string, SemaphoreSlim>`, acquired at step 1 and released in `finally`.

**Why not `Channel<CardJob>` + a single consumer per printer.** The brief asked for the reasoning either way, so explicitly: a channel is the right architecture when jobs must outlive the request that submitted them — batch printing, queue-and-walk-away, guaranteed delivery across restarts. None of those are requirements here. The Evolis endpoint is request/response by decision Q-26, matching the Matica endpoint it sits beside. Introducing a channel would force a job-status endpoint, job persistence across Agent restarts, an Angular polling change, and a second reconciliation mechanism alongside the outbox — all to solve a problem the SDK already solves at the device via session management and `Reserve()`. The semaphore adds one guarantee the SDK does not: a clean, immediate, localized "printer busy" instead of a raw `SESSION_EBUSY` surfacing mid-job.

**Partitioning.** By `DeviceName`, not by printer id — the Agent addresses hardware by device name and may see a device that is not registered.

**Printer failure.** The gate is released in `finally`; a failed job never poisons the printer. The connection scope closes, so the next job opens fresh.

**Agent restart.** Nothing to recover: no in-flight state is persisted, and the SDK releases the session after 45 s of inactivity. An in-flight card at restart is a physical problem for the operator, and the outbox still carries any unsent Backend Call #2 from before the restart.

**Duplicate prevention.** Three layers: the per-device gate prevents concurrent physical jobs; the idempotency key prevents duplicate backend records; and `resolve-for-print` is the backend's own guard against printing the same card item twice.

**Cancellation.** `CancellationToken` flows from the HTTP request through every await. A cancelled request releases the gate. In-flight SDK calls are not themselves cancellable, so cancellation takes effect at the next step boundary — **a card already being printed will finish printing.** This is honest behaviour, not a bug: the alternative is a jammed printer.

---

## 15. Testing Strategy

### 15.1 Split

| Runs without hardware | Requires hardware |
|---|---|
| All rendering (Phase 4) — geometry, colour, fonts, encoding, golden files | Bitmap prints legibly, lands at the right physical position |
| All status/return-code mapping (Phase 3) | Real flags appear as expected |
| All orchestration (Phase 5) against a fake `ICardPrinterAdapter` | Real timing, real failure modes |
| All API/schema work (Phases 1–2) | — |
| Event classification logic (Phase 6) | Real event names and payloads |
| Agent starts with no printer and no driver | — |

**Design consequence:** `ICardPrinterAdapter` exists partly so that the entire print flow — every branch, every failure — can be verified before hardware is available. Aim for the hardware phase to confirm, not to discover.

### 15.2 Per phase

| Phase | Unit | Integration | Hardware |
|---|---|---|---|
| 0 | — | — | **all** |
| 1 | service + validators | migration on populated DB, indexes, soft delete | — |
| 2 | partitioning, overwrite, idempotency | auth scope, branch mismatch, N+1 | optional |
| 3 | status mapper, return codes, registration client | starts with no printer | discovery, capabilities, registration |
| 4 | geometry, colour, panel, fonts, Arabic, golden file | — | deferred to 6 |
| 5 | twelve steps, every failure branch, concurrency, PAN hygiene | Matica regression | deferred to 6 |
| 6 | event classification | — | **the full matrix in §11** |

### 15.3 Regression guard

Every phase re-runs the existing Matica test suite. Decisions Q-26 and Q-27 make a Matica behaviour change a **defect by definition** — nothing in this plan should be able to alter it.

---

## 16. Hardware Validation Plan

| Stage | When | Needs | Gate |
|---|---|---|---|
| **HV-0** capability probe | before Phase 1 | Primacy 2 powered, connected, 1 encoded card | §5 answer sheet complete |
| **HV-1** discovery | end of Phase 3 | same | printer listed correctly; host registered |
| **HV-2** first print | end of Phase 5 | + mono ribbon, blank cards | a card comes out with a legible name |
| **HV-3** full matrix | Phase 6 | + colour ribbon, encoded cards, 50 blanks | every row of §11 passes |

**Materials needed on site:** one Primacy 2 with magnetic encoder and flip module; one mono (`KBLACK`) ribbon; one colour (`YMCKO`) ribbon; ≥ 50 blank CR-80 cards; ≥ 10 cards with valid track-2 data; ≥ 2 blank/unencoded cards for the negative test; network access from the supervising machine to the Inventory API.

**Sequencing note.** HV-0 is genuinely blocking. Phases 1, 2 and 4 can proceed without hardware, but Phase 3's configuration defaults and Phase 5's PAN extraction both depend on HV-0's answers. If hardware access is delayed, build 1, 2 and 4 first.

---

## 17. Architectural Decisions and Rationale

### Q-13 — Rebuild on Evolis.SDK 9.4.1
**Decided:** discard the ESPF implementation; build on the C# SDK.
**Why:** the ESPF reference has no magnetic code at all, and the flow requires a track-2 read; ESPF success detection is `result.Contains("OK")` against typed `ReturnCode`s and 256 named flags in the SDK; ESPF still requires Premium Suite locally (named pipe `.`/`ESPFServer00`), so it is no more LAN-friendly.
**Alternatives:** port ESPF (rejected — hand-rolled escape commands for magnetic, stringly-typed errors, a dead integration surface); Windows spooler/GDI (rejected — Evolis documents `ExtEscape(PASSTHROUGH)` as *"still in development"* for Primacy 2).
**Impact:** ~1,200 lines rewritten rather than copied; typed errors become possible, which is what makes the localized error catalogue work.

### Q-14 — Per-vendor print configuration
**Decided:** configuration is applied per printer/product type; no shared shape.
**Why:** Matica's `Cpi`/`OffsetX`/`OffsetY`/tipper parameters and Evolis's ribbon/face/orientation/font/colour set have no meaningful intersection.
**Alternatives:** a unified config table with nullable columns (rejected — half the columns wrong for every row, no DB-level integrity).
**Impact:** none on the database — `MaticaProductPrintConfigurations` and `EvolisProductPrintConfigurations` already exist separately. Confirms the adapter boundary.

### Q-15 — `EvolisPrinterConfigurations`
**Decided:** new 1:1 extension table.
**Why:** the Agent cannot address an Evolis printer without `DeviceName`, and there is nowhere to put it today. Trays, coercivity and the Q-29 switches are equally machine-specific.
**Alternatives:** agent-local `appsettings.json` (rejected — no central admin, no multi-branch visibility, drifts per machine); nullable columns on `Printer` (rejected, as Q-14).
**Impact:** one migration; overturns the documented "Evolis needs no extension table" assumption in `AppDbContext` and module requirement §1 — **needs a PM note.**

### Q-16 — Cardholder name only
**Decided:** no additional print fields in this scope.
**Why:** smallest correct scope; matches the Matica flow's single field.
**Impact:** `EvolisProductPrintConfiguration.ImageId` stays in the schema but is read by nothing. Adding card artwork later is a renderer change plus a way to ship image bytes to the Agent — no schema change.

### Q-17 — Application → SDK → Driver → Printer
**Decided:** the SDK talks to the printer through the installed driver.
**Why:** matches the supervision architecture (one machine hosts driver + EPS + Agent), and `GetDevices()` enumerates driver-installed devices — which is exactly what makes discovery machine-scoped for free.
**Alternatives:** driverless/URI-direct (rejected — would break `Device.isSupervised`, which the printer-list UI is built on); spooler (rejected, see Q-13).
**Impact:** the supervising machine must have the driver installed. `GetDevices()` will not see printers on other machines — which is the correct, intended behaviour.

### Q-19 — `RibbonTypes.SdkCode`
**Decided:** add a machine-facing column beside the operator-facing `Name`.
**Why:** `Name` is editable and will be localized or renamed; if it doubled as the SDK token, a rename would silently break printing.
**Alternatives:** seed `Name` with SDK tokens (rejected — one admin edit breaks production); hardcode a `Name`→token map in the Agent (rejected — two sources of truth, invisible to admins).
**Impact:** one column, one unique index, a three-step data migration, and a seed list from Phase 0.

### Q-20 — SkiaSharp
**Decided:** SkiaSharp for rendering.
**Why:** Microsoft's guidance is that `System.Drawing` is not supported in Windows services or ASP.NET — the Agent is both; GDI handle exhaustion is a known long-running-process failure mode; `System.Drawing.Common` is Windows-only from .NET 7.
**Alternatives:** keep `System.Drawing.Common` (rejected — the documented unsupported scenario is exactly ours; the reference `CardBitmap.cs` also carries a WPF `using` that would not compile); ImageSharp (rejected — Six Labors split licence would require a commercial licence for Modupay).
**Impact:** two packages; a rewritten renderer behind `ICardRenderer`; MIT licence; the reference bitmap code is not carried forward.

### Q-21 — Self-registration of `SupervisorHostName`
**Decided:** the Agent reports its host to the API on discovery.
**Why:** only the machine itself knows authoritatively which devices it can see; an admin-entered value would be stale from the moment hardware moves.
**Alternatives:** admin-entered (rejected — stale, manual); derived per request (rejected — the printer list must show it before any job runs).
**Impact:** one endpoint, one scoped credential change, one `IHostedService`. Registration failure is non-fatal by design.

### Q-22 — API source of truth + Agent runtime state
**Decided:** the API owns the registry; the Agent reports what it sees; Angular joins them.
**Why:** the SDK cannot see printers on other machines, so no single source can answer both questions. Registered assets are business data; discovered devices are runtime facts.
**Alternatives:** Agent as source of truth (rejected — an unreachable Agent would empty the printer list); API-only (rejected — the API cannot know what a machine sees).
**Impact:** four UI states (§4.4); the Agent never auto-creates a registered printer.

### Q-23 — Image-based printing
**Decided:** the name is rendered to a bitmap; the panel depends on the colour.
**Why:** the name may be printed in colour, and `SetBlack` writes the resin K panel only.
**Alternatives:** `SetBlack` always (rejected — cannot produce colour); `SetImage` always (rejected — documented as *"YMC panels only"*, so it cannot work on a mono ribbon).
**Impact:** the renderer emits a panel choice alongside the bytes; the adapter honours it; Q-25's compatibility check exists to catch the mismatch.

### Image format — BMP
**Decided:** BMP initially.
**Why:** the Primacy 1 implementation used BMP successfully against the same printer family; the SDK documents the parameter only as *"Binary data containing the image to print"* and does not list accepted encodings.
**Alternatives:** PNG (rejected — unverified, and no benefit at this size).
**Impact:** `ICardRenderer` returns `byte[]`, so a format change is one encoder line. Phase 0 step 8 confirms.

### Q-24 — Read-only magnetic, PAN = first 16 of track 2
**Decided:** read only; identical PAN extraction to Matica.
**Why:** keeps `resolve-for-print` byte-identical across vendors; no encoding requirement was stated.
**Alternatives:** read + write (rejected — no requirement; would add coercivity/format config and per-track data to the DTO); no magnetic (rejected — the flow needs to identify the card).
**Impact:** `MagSession` is used for one call. `ReadTracks` inserts the card itself, so **no LoadCard step** — a real simplification versus Matica.

### Q-25 — Colour-driven panel with a ribbon compatibility check
**Decided:** branch on `PrintColor`; if the loaded ribbon cannot satisfy it, fail with a clear message.
**Why:** the colour requirement comes from the product config, but the ribbon is a physical fact — and the two can disagree. Checking before printing means the operator gets an actionable message instead of a wasted card.
**Alternatives:** always colour (rejected — forces a colour ribbon everywhere, and resin gives sharper black text); ribbon-only (rejected — ignores the configured colour); no check (rejected — silent failure or a spoiled card).
**Impact:** `EvolisRibbonCompatibilityChecker`, two new localized errors, and a check positioned **after** resolve but **before** print, so no card is consumed on a mismatch.

### Q-26 — Separate endpoint
**Decided:** `Print-Card-Holder-Name-Evolis` as a new action; `PrintReqDto` untouched.
**Why:** lowest risk to a hardware-validated path; the two DTOs share only three fields, so a discriminated union would be mostly nullable.
**Alternatives:** one endpoint with a discriminator (rejected here — would change the validated Matica contract and force an Angular change on a working flow).
**Impact:** some duplication in the controller; Angular picks the endpoint from `Product.UsingPrinterType`.

### Q-27 — Add alongside, no Matica refactor
**Decided:** leave `MachineController.Print` exactly as it is.
**Why:** it is validated on real hardware; refactoring it is risk without immediate benefit.
**Alternatives:** extract the orchestrator first (rejected for now — a pure refactor phase with no feature delivery, requiring Matica hardware regression before Evolis work could begin).
**Impact — stated plainly:** two orchestration paths coexist; idempotency-key generation, the outbox write and the local-batch format are duplicated by hand and must be kept in sync. This weakens the extensibility goal until a future consolidation phase. **This is a known, accepted debt, recorded here so it is not rediscovered later as a surprise.**

### Q-28 — Reuse the reconciliation service account
**Decided:** extend it with `printer:register-supervisor`.
**Why:** the credential is already machine-to-machine, branch-scoped, revocable, needs no user session, and already runs on the same host.
**Alternatives:** a fourth dedicated signing key (rejected — more key-rotation surface for one extra write); user-triggered only (rejected — no host recorded until someone logs in).
**Impact:** one scope constant, one policy, one issuing-service change. Existing outbox reconciliation is unaffected.

### Q-29 — Configuration-driven `OpenMode` / `ErrorManagement`, default `DIRECT` + `SOFTWARE`
**Decided:** both are per-printer configuration; defaults are `DIRECT` and `SOFTWARE`.
**Why:** the reconciliation-correctness argument is decisive. Under EPS-managed errors, `Print()` blocks on a desktop popup; if it times out the card may still print, and the outbox would then record a failure for a physically successful card — breaking the exact guarantee the outbox exists to provide. But whether `SetErrorManagement(SOFTWARE)` is even accepted under EPS supervision cannot be determined from documentation (`ErrorManagement.SUPERVISED` is read-only), so hardcoding either choice risks an adapter rewrite.
**Alternatives:** hardcode `SUPERVISED` (rejected — the reconciliation risk above); hardcode `DIRECT` (rejected — unproven on this hardware, and may conflict with the stated supervision model).
**Impact:** two `tinyint` columns; `EvolisConnectionScope` reads them; a bad Phase 0/6 result costs a configuration change, not a redesign. `Device.isSupervised` may report `false` under `DIRECT` — the discovery response therefore returns `isSupervised` and `INF_PCSVC_LOCKED` as two independent signals.

### Q-30 — API contracts only
**Decided:** no Angular implementation.
**Why:** no access to the Angular repository; inventing file paths would violate the plan's own rule.
**Impact:** every phase specifies request/response shapes and UI states, never Angular files.

### Q-31 — Inline code, not patches
**Decided:** code is delivered in the chat per phase and applied manually.
**Impact:** phases are scoped so each fits one session; every phase names its files with `CREATE` / `MODIFY` / `DELETE`.

### Concurrency — `SemaphoreSlim`, not `Channel`
**Proposed in this plan** (flagged so it can be overturned): per-device semaphore rather than a queued single-consumer model.
**Why:** full reasoning in §14. In short — a channel solves job durability, which is not a requirement, and would force a submit-then-poll API contract that contradicts Q-26.
**Impact:** the endpoint stays synchronous like Matica's. The orchestrator is already the right seam if queued batches are ever needed.

---

## 18. Risks and Open Issues

### 18.1 Risks

| # | Risk | Impact | Mitigation | Resolved by |
|---|---|---|---|---|
| R-1 | `SetErrorManagement(SOFTWARE)` refused under EPS | blocking popups; false failures in the outbox | Q-29 config switch; longer timeouts | Phase 0 step 6 |
| R-2 | `Reserve()` returns `SESSION_EBUSY` under EPS | cannot lock the printer for a job | `ReserveOnJob=false`; rely on SDK session management | Phase 0 step 5 |
| R-3 | BMP rejected by `SetImage`/`SetBlack` | nothing prints | `ICardRenderer` returns `byte[]`; swap encoder | Phase 0 step 8 |
| R-4 | Track 2 returns ISO sentinels | wrong PAN → every resolve fails | `IPanExtractor` strips them | Phase 0 step 9 |
| R-5 | `RibbonType` token set differs from the assembly strings | seed data wrong | reflection dump | Phase 0 step 13 |
| R-6 | Configured font absent on the supervising machine | wrong glyphs, or blank Arabic | default + warning; verify fonts at deployment | Phase 4 / HV-2 |
| R-7 | Duplex `SetImage(BACK,…)` implicitly sets `GDuplexType` | conflicting settings | set `GDuplexType` explicitly and assert | Phase 5 / HV-3 |
| R-8 | 45 s session timeout fires during a long duplex print | job aborts mid-card | measure real duration | Phase 6 soak |
| R-9 | Matica/Evolis duplicated orchestration drifts (Q-27) | divergent outbox or idempotency behaviour | cross-reference comments in both; a future consolidation phase | accepted debt |
| R-10 | Module requirement §1 contradicts Q-15 | spec/code mismatch | PM note with the migration | Phase 1 |
| R-11 | Two machines claim the same device | host flip-flopping | last-writer-wins + warning log; unique index per host | Phase 2 |
| R-12 | Agent runs in Session 0 with no interactive user | driver access may behave differently | test explicitly as a service with nobody logged in | Phase 3 acceptance |

### 18.2 Open issues (tracked, not blocking)

- `ImageId` on `EvolisProductPrintConfiguration` is unused under Q-16. Leave it; revisit if card artwork is requested.
- `Controllers/HardwareIoPocController.cs` in the Agent is a temporary POC with no `[Authorize]`, marked for deletion by its own author. Out of scope here, but it should not ship — raise separately.
- Matica consolidation behind `ICardPrinterAdapter` is deferred by Q-27. Worth a future phase.
- Chip/contactless encoding is **impossible** on this LAN-only deployment (*"no smart contact / contactless encoding option is compatible with Ethernet communication"*). If ever required, it needs USB or a USB-over-IP module — a deployment change, not a code change.

---

## 19. Final End-to-End Flow

**Operator prints a card at branch 12.**

```
 1  Operator opens the printing screen.
 2  Angular → GET /api/printers?branchId=12           (Inventory API — registered set)
 3  Angular → GET /api/machine/evolis/printers        (Agent — discovered set)
 4  Angular joins on DeviceName → "Supervised here / Offline /
      Registered but not on this machine / Discovered but not registered"
 5  Operator picks a product. Product.UsingPrinterType == Evolis
      → Angular calls the Evolis endpoint (Q-26).
 6  Angular → GET /api/products/{id}/print-config     → Evolis config fields
 7  Angular → Inventory API mints a print-agent token (branch- and printer-scoped)
 8  Angular → POST http://localhost:8403/api/Machine/Print-Card-Holder-Name-Evolis
              Bearer <print-agent token>
              { CardHolderName, DeviceName, ProductId, BranchId, + print config }

  ── Agent ───────────────────────────────────────────────────────────────────
 9  [Authorize] validates the token; branch claim vs body checked
10  per-device gate acquired                                → else PrinterBusy
11  Connection opened  (OpenMode from config, Q-29)
      SetErrorManagement(config)  ·  Reserve()  if enabled
12  GetStatus → ready gate: no errors, no blocking warnings,
      no card inside, CFG_MAGNETIC present                  → else NotReady(specific)
13  MagSession.ReadTracks(t2) → GetData(TRACK2)             → else MagBlank / MagUnreadable → reject
14  PAN = sentinel-stripped, first 16 chars

  ── Backend Call #1 ─────────────────────────────────────────────────────────
15  POST /api/print-flow/resolve-for-print { pan, productId, branchId }
      → { productItemId, maskedPan, holderName }            → else reject + fail
      raw PAN is discarded here and never referenced again

  ── Agent ───────────────────────────────────────────────────────────────────
16  GetRibbonInfo → compatibility check (Q-25)
      ribbon ≠ configured            → RibbonMismatch       → reject, no card consumed
      colour needed, mono ribbon     → RibbonCannotPrintColour
17  ICardRenderer → 1016×648 (or 648×1016) BMP + panel choice
18  PrintSession: Init · GRibbonType · trays · GDuplexType · SetAutoEject(false)
      SetBlack(face,bmp)  or  SetImage(face,bmp)
      Print(PrintTimeoutMs) → ReturnCode
      ENEEDACTION → GetEvent → classify → SetEvent(RETRY|CANCEL)
19  encrypted line appended to LocalBatch.lbt (maskedPan, name, branch, tenant, status, product)
20  success → EjectCard()        failure → RejectCard()

  ── Backend Call #2 ─────────────────────────────────────────────────────────
21  POST /api/print-flow/record-print-result
      { branchId, productItemId, success, holderName, idempotencyKey }
      transient failure → IOutboxStore entry; Hangfire reconciliation completes it later

  ── Agent ───────────────────────────────────────────────────────────────────
22  finally: Release() · Dispose() · gate released
23  ApiResponse<PrintResult> returned; any error localized EN / Egyptian Arabic
```

**What never changed:** the Inventory API's print-flow contract, the outbox, the idempotency mechanism, the print-agent token flow, the response envelope, the localization filter, and every Matica endpoint. That is the measure of whether this design is correct.
