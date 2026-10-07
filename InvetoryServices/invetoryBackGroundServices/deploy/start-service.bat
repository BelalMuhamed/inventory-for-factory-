@echo off
rem Starts the Matica Printer Agent Windows Service. Requires an elevated
rem (Administrator) command prompt.
set SERVICE_NAME=InvetoryServices
sc start %SERVICE_NAME%
pause
