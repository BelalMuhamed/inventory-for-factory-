@echo off
rem Stops the Matica Printer Agent Windows Service. Requires an elevated
rem (Administrator) command prompt. Does not affect its automatic-recovery
rem configuration - sc failure's restart actions only trigger on an
rem unexpected process exit, not on a deliberate `sc stop`.
set SERVICE_NAME=InvetoryServices
sc stop %SERVICE_NAME%
pause
