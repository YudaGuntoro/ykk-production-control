@echo off
setlocal

set "PROJECT=%~dp0ProductionControl.WebAPI.csproj"
set "OUTPUT=%~dp0publish"

echo Publishing backend for IIS...
echo Project: %PROJECT%
echo Output : %OUTPUT%
echo.

if not exist "%OUTPUT%" mkdir "%OUTPUT%"
if errorlevel 1 goto failed

dotnet restore "%PROJECT%"
if errorlevel 1 goto failed

dotnet publish "%PROJECT%" -c Release -o "%OUTPUT%" --no-restore
if errorlevel 1 goto failed

echo.
echo Restarting IIS...
iisreset
if errorlevel 1 (
    echo.
    echo IIS reset failed. Run this batch as Administrator or run iisreset manually.
) else (
    echo IIS reset completed.
)

echo.
echo Backend publish completed.
echo IIS publish folder:
echo %OUTPUT%
pause
exit /b 0

:failed
echo.
echo Backend publish failed.
pause
exit /b 1
