@echo off
rem Shows the Matica Printer Agent Windows Service's current state and its
rem configured automatic-recovery actions. Does not require elevation -
rem `sc query`/`sc qfailure` are read-only.
set SERVICE_NAME=InvetoryServices

echo === Service state ===
sc query %SERVICE_NAME%
if errorlevel 1 (
    echo No service named %SERVICE_NAME% is registered - run install-service.bat.
    pause
    exit /b 1
)

echo.
echo === Automatic-recovery configuration ===
sc qfailure %SERVICE_NAME%

pause
