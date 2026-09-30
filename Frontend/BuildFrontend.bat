@echo off
setlocal

set "FRONTEND_DIR=%~dp0"
set "PM2_APP_NAME=ykk-frontend"
set "FRONTEND_PORT=3000"
set "PM2_AVAILABLE=0"

echo Building frontend...
cd /d "%FRONTEND_DIR%"
if errorlevel 1 goto failed

where node >nul 2>nul
if errorlevel 1 (
    echo Node.js was not found. Install Node.js LTS first.
    goto failed
)

where npm >nul 2>nul
if errorlevel 1 (
    echo npm was not found. Install Node.js LTS first.
    goto failed
)

where pm2 >nul 2>nul
if errorlevel 1 (
    set "PM2_AVAILABLE=0"
) else (
    set "PM2_AVAILABLE=1"
)

if "%PM2_AVAILABLE%"=="1" (
    echo.
    echo Stopping PM2 app %PM2_APP_NAME% if it is running...
    call pm2 stop "%PM2_APP_NAME%" >nul 2>nul
)

echo.
echo Installing frontend dependencies...
call npm install
if errorlevel 1 goto failed

echo.
echo Building Next.js frontend...
call npm run build
if errorlevel 1 goto failed

if "%PM2_AVAILABLE%"=="0" goto skip_pm2

echo.
echo Restarting frontend with PM2...
call pm2 delete "%PM2_APP_NAME%" >nul 2>nul
call pm2 start node --name "%PM2_APP_NAME%" -- node_modules\next\dist\bin\next start -p %FRONTEND_PORT%
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
echo URL: http://localhost:%FRONTEND_PORT%
pause
exit /b 0

:failed
echo.
echo Frontend build failed.
if "%PM2_AVAILABLE%"=="1" (
    echo Trying to restart existing PM2 app %PM2_APP_NAME%...
    call pm2 restart "%PM2_APP_NAME%" >nul 2>nul
)
pause
exit /b 1
