@echo off
setlocal

set "PROJECT=%~dp0ProductionControl.WebAPI.csproj"
set "OUTPUT=%~dp0publish"
set "STOPPED_IIS=0"

echo Publishing backend for IIS...
echo Project: %PROJECT%
echo Output : %OUTPUT%
echo.

if not exist "%OUTPUT%" mkdir "%OUTPUT%"
if errorlevel 1 goto failed

dotnet restore "%PROJECT%"
if errorlevel 1 goto failed

echo.
echo Stopping IIS before publish...
iisreset /stop
if errorlevel 1 (
    echo.
    echo Failed to stop IIS. Run this batch as Administrator.
    goto failed
)
set "STOPPED_IIS=1"

dotnet publish "%PROJECT%" -c Release -o "%OUTPUT%" --no-restore
if errorlevel 1 goto failed

echo.
echo Starting IIS...
iisreset /start
if errorlevel 1 (
    echo.
    echo Failed to start IIS. Run this batch as Administrator or run iisreset /start manually.
    goto failed
) else (
    echo IIS start completed.
)
set "STOPPED_IIS=0"

echo.
echo Backend publish completed.
echo IIS publish folder:
echo %OUTPUT%
pause
exit /b 0

:failed
echo.
if "%STOPPED_IIS%"=="1" (
    echo Starting IIS after failure...
    iisreset /start
)
echo Backend publish failed.
pause
exit /b 1
