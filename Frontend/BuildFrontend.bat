@echo off
setlocal

set "FRONTEND_DIR=%~dp0"

echo Building frontend...
cd /d "%FRONTEND_DIR%"
if errorlevel 1 goto failed

if not exist "%FRONTEND_DIR%.next" mkdir "%FRONTEND_DIR%.next"
if errorlevel 1 goto failed

if exist package-lock.json (
    call npm ci
) else (
    call npm install
)
if errorlevel 1 goto failed

call npm run build
if errorlevel 1 goto failed

echo.
echo Frontend build completed.
pause
exit /b 0

:failed
echo.
echo Frontend build failed.
pause
exit /b 1
