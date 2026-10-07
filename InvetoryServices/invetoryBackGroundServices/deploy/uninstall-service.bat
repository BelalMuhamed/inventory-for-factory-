@echo off
setlocal

rem ============================================================================
rem Matica Printer Agent - Windows Service uninstall script
rem ============================================================================
rem Must be run from an elevated (Administrator) command prompt.

set SERVICE_NAME=InvetoryServices

sc query %SERVICE_NAME% >nul 2>&1
if errorlevel 1 (
    echo No service named %SERVICE_NAME% is registered - nothing to remove.
    pause
    exit /b 0
)

echo Stopping %SERVICE_NAME% (if running)...
sc stop %SERVICE_NAME% >nul 2>&1

rem sc stop is asynchronous - give the process a moment to actually exit
rem before deleting the service definition out from under it.
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

echo Service removed. Its automatic-recovery configuration (sc failure) is
echo removed along with it - reinstalling will need install-service.bat again,
echo not a separate recovery-configuration step.
pause
