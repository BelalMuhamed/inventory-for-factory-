# Matica Printer Agent — Deployment Guide

## Prerequisites

- Windows, .NET 8 runtime (the published output is self-contained only if
  published that way — if not, the target machine needs the .NET 8 ASP.NET
  Core runtime installed).
- Administrator rights on the target machine, for the install/uninstall/
  start/stop/restart scripts (`sc create`/`sc failure`/`sc start`/`sc stop`
  all require elevation; `status-service.bat` does not).
- Network reachability from this machine to:
  - The Matica S3300e printer's LAN IP (whatever port your printer config
    supplies per-request).
  - The Inventory API's base URL (`InventoryApi:BaseUrl`).
- A **published** build — publish via the `FolderProfile` publish profile
  (`invetoryBackGroundServices/Properties/PublishProfiles/FolderProfile.pubxml`),
  which outputs to `bin\Release\net8.0\publish\` inside the project folder.
  This repo also has a separate, older `publish\` folder at the repo root
  containing an earlier build and the original install scripts — **that
  folder is not what `dotnet publish` actually produces with the profile
  currently configured**, so don't assume it's up to date. Republish before
  deploying rather than trusting whichever `publish\` folder happens to be
  present.

## First-time install on a new machine

1. Publish the project (Visual Studio's Publish action, using the
   `FolderProfile`, or `dotnet publish -c Release`).
2. Copy the **entire** publish output folder to the target machine — the
   `.exe`, `appsettings.json`, all dependency DLLs.
3. Copy this `deploy\` folder's scripts alongside the executable (or run
   them from `deploy\` with the executable's path adjusted — the scripts
   assume they sit next to `invetoryBackGroundServices.exe`; see each
   script's own comments).
4. Fill in `appsettings.json`'s non-secret values (see the checklist below).
5. Set every required secret via `dotnet user-secrets set` **on the
   development machine before publishing** (user-secrets are a developer
   convenience tied to a project, not something that ships to production)
   — or, for a real production deployment, set each one as an **environment
   variable** on the target machine instead (the same fail-fast checks in
   `Program.cs` read from either source; user-secrets don't exist outside
   a dev machine at all). See the checklist below for the exact list and
   the double-underscore convention environment variables need.
6. Run `install-service.bat` as Administrator.
7. Run `status-service.bat` to confirm it started; if it didn't, check the
   `AppLog` folder next to the executable — the fail-fast checks throw a
   clear message naming exactly which configuration value is missing.

## Configuration checklist

Every value below has been introduced across this project's phases; this
is the complete list in one place; get any one of them wrong and the
service throws a clear fail-fast error at startup (except an item marked
non-fatal, and except the Inventory API's own values, which live on that
project, not this one).

**Non-secret — `appsettings.json`:**

| Key | Meaning |
|---|---|
| `Cors:AllowedOrigins` | Exact origin(s) allowed to call this service (e.g. Angular's URL). Empty fails startup on purpose. |
| `MachineCommunication:TimeoutSeconds` | Per-command timeout talking to the physical printer. |
| `Outbox:Directory` | Where pending reconciliation entries are written. Blank defaults to an `Outbox` folder next to the executable — fine for one machine, worth setting explicitly if you ever need to find it without guessing. |
| `InventoryApi:BaseUrl` | The Inventory API's base URL this service calls. |
| `PrintAgentAuth:Issuer` / `Audience` | Must match the Inventory API's `PrintAgentToken:Issuer`/`Audience` exactly. |
| `ReconciliationCredential:ClientId` | The GUID from provisioning a `PrintAgentServiceAccount` (see the reconciliation-credential phase) — not a secret itself, but only meaningful once that account has actually been provisioned on the Inventory API. |

**Secret — user-secrets (dev) or environment variable (production), never in `appsettings.json`:**

| Key | Meaning |
|---|---|
| `PrintAgentAuth:SigningKey` | Must be byte-for-byte identical to the Inventory API's `PrintAgentToken:SigningKey`. |
| `ReconciliationCredential:ClientSecret` | The one-time secret returned when the service account was provisioned via `POST api/auth/service-accounts` on the Inventory API. Lost means re-provision, not recover. |

As an environment variable, a colon-separated key like `PrintAgentAuth:SigningKey`
becomes `PrintAgentAuth__SigningKey` (double underscore) on most platforms —
this is .NET configuration's own convention, not specific to this service.

## Scripts in this folder

| Script | Requires elevation | Purpose |
|---|---|---|
| `install-service.bat` | Yes | Registers the service, configures automatic crash recovery, starts it. Refuses to run if the service already exists (uninstall first). |
| `uninstall-service.bat` | Yes | Stops (if running) and removes the service, including its recovery configuration. |
| `start-service.bat` | Yes | Starts an already-installed, stopped service. |
| `stop-service.bat` | Yes | Stops a running service. Does not affect automatic recovery — that only triggers on an unexpected exit, not a deliberate stop. |
| `restart-service.bat` | Yes | Stops, waits for a clean STOPPED state, then starts — avoids racing a slow shutdown. |
| `status-service.bat` | No | Shows current state and the configured recovery actions. Safe to run anytime, including to verify install-service.bat actually configured recovery correctly. |

## Automatic recovery

`install-service.bat` configures `sc failure` to restart the process 5
seconds after each of its first three unexpected exits within a rolling
24-hour window, then stop trying. This did not exist before this phase —
confirmed by reading the repo's previous install script, which only ever
called `sc create ... start= auto` with no failure-recovery configuration
at all, so a crash previously just left the service down until someone
noticed. A service that keeps crashing past three attempts needs a human
looking at the log, not a longer retry list.

## Verifying a deployment end to end

1. `status-service.bat` — confirm `RUNNING` and that `sc qfailure` shows
   the restart actions configured above.
2. Trigger a real print (or at minimum a `GET /api/Machine/get-machine-info`
   call) and confirm it succeeds against the actual printer.
3. Confirm the reconciliation startup scan ran — check `AppLog` for an
   "Outbox sweep" line right after the service started (only appears if
   there was at least one pending entry; an empty outbox logs nothing on
   that specific line, which is expected, not a failure).
4. Simulate a crash (`taskkill /F` on the process, not `stop-service.bat` —
   a deliberate stop doesn't exercise recovery) and confirm the service
   comes back on its own within a few seconds, per the recovery
   configuration above.

## Known gap not addressed by this phase

This repository has `bin/`, `obj/`, and `.vs/` in `.gitignore`, but a large
number of files under `invetoryBackGroundServices/bin/Release/net8.0/publish/`
and the repo-root `publish/` folder — including compiled DLLs, the `.exe`,
and actual runtime log files under `publish/AppLog/` — are already tracked
in git history from before the `.gitignore` was added (a `.gitignore` entry
never retroactively untracks a file). This is a real repo-hygiene issue
worth a deliberate cleanup pass (`git rm --cached` plus a history rewrite
if the tracked log contents are considered sensitive), flagged here rather
than fixed silently as part of this phase, since rewriting tracked binary/
log history is a bigger, more disruptive decision than adding install
scripts and deserves its own explicit sign-off.
