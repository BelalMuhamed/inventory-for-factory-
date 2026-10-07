@echo off
setlocal enabledelayedexpansion

rem ============================================================================
rem Matica Printer Agent - Windows Service install script
rem ============================================================================
rem Must be run from an elevated (Administrator) command prompt - `sc create`
rem and `sc failure` both require administrator rights.
rem
rem Fixed here: SERVICE_NAME used to be "MyWindowsServiceApp", which did not
rem match Program.cs's AddWindowsService ServiceName ("InvetoryServices") -
rem the two identities were out of sync, risking confusing Event Viewer
rem entries and making the SCM name and the app's own internal name
rem impossible to correlate. Also added: automatic-recovery configuration
rem (sc failure), which did not exist at all before - a crash simply left
rem the service down until someone noticed. See ..\deploy\README.md for the
rem canonical, maintained copy of these scripts and the full deployment
rem checklist - this file is kept in sync with that one.

set SERVICE_NAME=InvetoryServices
set SERVICE_DISPLAY_NAME=Matica Printer Agent (InvetoryServices)
set SERVICE_DESCRIPTION=Drives the Matica S3300e card printer over the LAN and reconciles print results with the Inventory API.
set EXE_PATH=%~dp0invetoryBackGroundServices.exe

echo Installing %SERVICE_NAME% service...
echo   Executable: %EXE_PATH%
echo.

if not exist "%EXE_PATH%" (
    echo ERROR: %EXE_PATH% was not found. Run this script from the folder
    echo containing invetoryBackGroundServices.exe.
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
rem failures within a rolling 24-hour window, then stop trying.
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
    echo Check the AppLog folder next to the executable for why - a missing
    echo signing key or credential is the most likely cause.
    exit /b 1
)

echo.
echo Service installed, configured for automatic recovery, and started.
pause
