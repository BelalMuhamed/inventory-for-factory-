@echo off
setlocal

rem ============================================================================
rem Matica Printer Agent - Windows Service uninstall script
rem ============================================================================
rem Must be run from an elevated (Administrator) command prompt.
rem
rem Fixed here: SERVICE_NAME used to be "MyWindowsServiceApp" - see
rem install-service.bat's own comment for why that was wrong. Also now stops
rem the service and waits for it to actually reach STOPPED before deleting
rem it, instead of stopping and deleting in the same breath (sc stop is
rem asynchronous, so the previous version could race a still-running process).

set SERVICE_NAME=InvetoryServices

sc query %SERVICE_NAME% >nul 2>&1
if errorlevel 1 (
    echo No service named %SERVICE_NAME% is registered - nothing to remove.
    pause
    exit /b 0
)

echo Stopping %SERVICE_NAME% (if running)...
sc stop %SERVICE_NAME% >nul 2>&1

:wait_stopped
sc query %SERVICE_NAME% | find "STOPPED" >nul
if errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_stopped
)

echo Removing %SERVICE_NAME%...
sc delete %SERVICE_NAME%
if errorlevel 1 (
    echo ERROR: sc delete failed - see the output above.
    exit /b 1
)

echo Service removed.
pause
