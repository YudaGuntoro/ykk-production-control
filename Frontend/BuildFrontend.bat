@echo off
setlocal

set "FRONTEND_DIR=%~dp0"
set "PM2_APP_NAME=ykk-frontend"
set "FRONTEND_PORT=3000"
set "PM2_AVAILABLE=0"
set "PM2_CMD="
set "STANDALONE_DIR=%FRONTEND_DIR%.next\standalone"

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

for /f "delims=" %%i in ('where pm2 2^>nul') do if not defined PM2_CMD set "PM2_CMD=%%i"
if not defined PM2_CMD if exist "%APPDATA%\npm\pm2.cmd" set "PM2_CMD=%APPDATA%\npm\pm2.cmd"
if defined PM2_CMD (
    set "PM2_AVAILABLE=1"
) else (
    set "PM2_AVAILABLE=0"
)

if "%PM2_AVAILABLE%"=="1" (
    echo.
    echo Stopping PM2 app %PM2_APP_NAME% if it is running...
    call "%PM2_CMD%" stop "%PM2_APP_NAME%" >nul 2>nul
)

echo.
echo Installing frontend dependencies...
call npm install
if errorlevel 1 goto failed

echo.
echo Building Next.js frontend...
call npm run build
if errorlevel 1 goto failed

if not exist "%STANDALONE_DIR%\server.js" (
    echo.
    echo Next.js standalone server was not found:
    echo %STANDALONE_DIR%\server.js
    goto failed
)

echo.
echo Copying standalone static assets...
if exist "%STANDALONE_DIR%\.next\static" rmdir /s /q "%STANDALONE_DIR%\.next\static"
xcopy "%FRONTEND_DIR%.next\static" "%STANDALONE_DIR%\.next\static\" /E /I /Y >nul
if errorlevel 1 goto failed

if exist "%FRONTEND_DIR%public" (
    if exist "%STANDALONE_DIR%\public" rmdir /s /q "%STANDALONE_DIR%\public"
    xcopy "%FRONTEND_DIR%public" "%STANDALONE_DIR%\public\" /E /I /Y >nul
    if errorlevel 1 goto failed
)

if "%PM2_AVAILABLE%"=="0" goto skip_pm2

echo.
echo Restarting frontend with PM2...
call "%PM2_CMD%" delete "%PM2_APP_NAME%" >nul 2>nul
set "PORT=%FRONTEND_PORT%"
call "%PM2_CMD%" start "%ProgramFiles%\nodejs\node.exe" --name "%PM2_APP_NAME%" --cwd "%STANDALONE_DIR%" -- server.js
if errorlevel 1 goto failed

call "%PM2_CMD%" save
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
    call "%PM2_CMD%" restart "%PM2_APP_NAME%" >nul 2>nul
)
pause
exit /b 1
