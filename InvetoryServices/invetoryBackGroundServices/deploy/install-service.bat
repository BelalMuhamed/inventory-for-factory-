@echo off
setlocal enabledelayedexpansion

rem ============================================================================
rem Matica Printer Agent - Windows Service install script
rem ============================================================================
rem Must be run from an elevated (Administrator) command prompt - `sc create`
rem and `sc failure` both require administrator rights; a non-elevated run
rem fails with "Access is denied" from `sc`, not from this script.
rem
rem Run this from the folder containing invetoryBackGroundServices.exe (i.e.
rem the actual publish output - see the deploy/README.md in this same folder
rem for exactly which folder that is and why it needs saying explicitly).

rem Service name registered with the Service Control Manager. Must match
rem AddWindowsService's ServiceName in Program.cs ("InvetoryServices") - the
rem two were previously out of sync (this script used to register
rem "MyWindowsServiceApp" while the running process identified itself
rem internally as "InvetoryServices"), which risked confusing Event Viewer
rem entries and made the SCM name and the app's own name impossible to
rem correlate. Confirmed by reading the actual previous script, not assumed.
set SERVICE_NAME=InvetoryServices
set SERVICE_DISPLAY_NAME=Matica Printer Agent (InvetoryServices)
set SERVICE_DESCRIPTION=Drives the Matica S3300e card printer over the LAN and reconciles print results with the Inventory API.
set EXE_PATH=%~dp0invetoryBackGroundServices.exe

echo Installing %SERVICE_NAME% service...
echo   Executable: %EXE_PATH%
echo.

if not exist "%EXE_PATH%" (
    echo ERROR: %EXE_PATH% was not found.
    echo Run this script from the folder containing invetoryBackGroundServices.exe,
    echo not from the repo's deploy\ folder directly.
    exit /b 1
)

sc query %SERVICE_NAME% >nul 2>&1
if not errorlevel 1 (
    echo ERROR: a service named %SERVICE_NAME% already exists.
    echo Run uninstall-service.bat first if you intend to reinstall it.
    exit /b 1
)

sc create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto DisplayName= "%SERVICE_DISPLAY_NAME%"
if errorlevel 1 (
    echo ERROR: sc create failed - see the output above.
    exit /b 1
)

sc description %SERVICE_NAME% "%SERVICE_DESCRIPTION%"

rem Automatic recovery: restart 5 seconds after each of the first three
rem failures within a rolling 24-hour (86400-second) window, then stop
rem trying - a persistently crashing service needs a human to look at it,
rem not an infinite restart loop that masks the underlying problem.
rem
rem This was entirely absent before this script existed in this form -
rem confirmed by reading the previous version, which called only
rem `sc create ... start= auto` with no `sc failure` configured at all, so
rem a crash simply left the service down until someone happened to notice.
sc failure %SERVICE_NAME% reset= 86400 actions= restart/5000/restart/5000/restart/5000
if errorlevel 1 (
    echo WARNING: sc failure did not apply cleanly - the service was created
    echo but will not restart automatically after a crash. Re-run:
    echo   sc failure %SERVICE_NAME% reset= 86400 actions= restart/5000/restart/5000/restart/5000
)

echo.
echo Starting %SERVICE_NAME%...
sc start %SERVICE_NAME%
if errorlevel 1 (
    echo WARNING: the service was created and configured, but failed to start.
    echo Check its status with status-service.bat and the AppLog folder next to
    echo the executable for why - a missing signing key or credential (see the
    echo appsettings.json fail-fast checks in Program.cs) is the most likely cause.
    exit /b 1
)

echo.
echo Service installed, configured for automatic recovery, and started.
echo Use start-service.bat / stop-service.bat / restart-service.bat / status-service.bat
echo to control it, or uninstall-service.bat to remove it entirely.
pause
