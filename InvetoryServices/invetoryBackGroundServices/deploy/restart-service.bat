@echo off
setlocal

rem Restarts the Matica Printer Agent Windows Service. Requires an elevated
rem (Administrator) command prompt.
set SERVICE_NAME=InvetoryServices

echo Stopping %SERVICE_NAME%...
sc stop %SERVICE_NAME% >nul 2>&1

rem Poll for STOPPED rather than a fixed sleep, so this doesn't wait longer
rem than necessary on a fast shutdown or fail on a slow one - `sc start`
rem issued while the previous instance is still mid-shutdown can otherwise
rem race it.
:wait_stopped
sc query %SERVICE_NAME% | find "STOPPED" >nul
if errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_stopped
)

echo Starting %SERVICE_NAME%...
sc start %SERVICE_NAME%
if errorlevel 1 (
    echo WARNING: restart failed - check status-service.bat and the AppLog folder.
    exit /b 1
)

echo %SERVICE_NAME% restarted.
pause
