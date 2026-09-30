@echo off
setlocal

set "FRONTEND_DIR=%~dp0"
set "PM2_APP_NAME=ykk-frontend"
set "FRONTEND_PORT=3000"

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

where pm2 >nul 2>nul
if errorlevel 1 goto skip_pm2

pm2 describe "%PM2_APP_NAME%" >nul 2>nul
if errorlevel 1 (
    echo.
    echo PM2 app not found. Starting %PM2_APP_NAME% on port %FRONTEND_PORT%...
    call pm2 start node --name "%PM2_APP_NAME%" -- node_modules\next\dist\bin\next start -p %FRONTEND_PORT%
) else (
    echo.
    echo Restarting PM2 app %PM2_APP_NAME%...
    call pm2 restart "%PM2_APP_NAME%"
)
if errorlevel 1 goto failed

call pm2 save
if errorlevel 1 goto failed
goto build_done

:skip_pm2
echo.
echo PM2 was not found. Build completed without restarting frontend service.

:build_done
echo.
echo Frontend build completed.
pause
exit /b 0

:failed
echo.
echo Frontend build failed.
pause
exit /b 1
