@echo off
setlocal

set "FRONTEND_DIR=%~dp0"
set "PM2_APP_NAME=ykk-frontend"
set "FRONTEND_PORT=3000"
set "PM2_AVAILABLE=0"
set "PM2_CMD="
set "STANDALONE_DIR=%FRONTEND_DIR%.next\standalone"
set "LOCKED_STANDALONE_DIR=%FRONTEND_DIR%.next\standalone.locked-%RANDOM%"
if not defined PM2_HOME set "PM2_HOME=%USERPROFILE%\.pm2"

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
    echo Stopping PM2 app %PM2_APP_NAME% before build...
    call "%PM2_CMD%" delete "%PM2_APP_NAME%" >nul 2>nul
    call "%PM2_CMD%" kill >nul 2>nul
)

echo.
echo Releasing port %FRONTEND_PORT% if it is still locked...
for /f "tokens=5" %%p in ('netstat -ano ^| findstr /R /C:":%FRONTEND_PORT% .*LISTENING"') do (
    if not "%%p"=="0" (
        echo Stopping process %%p on port %FRONTEND_PORT%...
        taskkill /PID %%p /F >nul 2>nul
    )
)
timeout /t 2 /nobreak >nul

if exist "%STANDALONE_DIR%" (
    echo.
    echo Removing previous standalone build folder...
    call :remove_standalone
    if exist "%STANDALONE_DIR%" (
        echo.
        echo Standalone folder is still locked. Trying to rename old folder...
        move "%STANDALONE_DIR%" "%LOCKED_STANDALONE_DIR%" >nul 2>nul
        if exist "%STANDALONE_DIR%" (
            call :force_unlock_standalone
        )
        if exist "%STANDALONE_DIR%" (
            echo.
            echo Could not remove locked standalone folder:
            echo %STANDALONE_DIR%
            echo Close any CMD/Explorer window opened inside this folder, then run this batch again.
            goto failed
        )
        echo Old standalone folder was moved to:
        echo %LOCKED_STANDALONE_DIR%
    )
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

:remove_standalone
rmdir /s /q "%STANDALONE_DIR%" >nul 2>nul
if not exist "%STANDALONE_DIR%" exit /b 0

echo Standalone folder is still locked. Stopping frontend node processes...
taskkill /IM node.exe /F >nul 2>nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "$root = '%FRONTEND_DIR:\=\\%'; Get-CimInstance Win32_Process -Filter \"Name = 'node.exe'\" | Where-Object { $_.CommandLine -like ('*' + $root + '*') -or $_.CommandLine -like '*.next\\standalone\\server.js*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" >nul 2>nul
call :kill_locking_processes
timeout /t 2 /nobreak >nul

for /l %%r in (1,1,5) do (
    if exist "%STANDALONE_DIR%" (
        rmdir /s /q "%STANDALONE_DIR%" >nul 2>nul
        if exist "%STANDALONE_DIR%" timeout /t 2 /nobreak >nul
    )
)
exit /b 0

:force_unlock_standalone
echo Standalone folder is still locked. Restarting Explorer and retrying cleanup...
call :kill_locking_processes
taskkill /F /IM explorer.exe >nul 2>nul
timeout /t 2 /nobreak >nul
attrib -R -S -H "%STANDALONE_DIR%\*" /S /D >nul 2>nul
rmdir /s /q "%STANDALONE_DIR%" >nul 2>nul
if exist "%STANDALONE_DIR%" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Remove-Item -LiteralPath '%STANDALONE_DIR%' -Recurse -Force -ErrorAction SilentlyContinue" >nul 2>nul
)
if exist "%STANDALONE_DIR%" (
    move "%STANDALONE_DIR%" "%LOCKED_STANDALONE_DIR%" >nul 2>nul
)
start explorer.exe
timeout /t 1 /nobreak >nul
exit /b 0

:kill_locking_processes
powershell -NoProfile -ExecutionPolicy Bypass -File "%FRONTEND_DIR%KillLockedStandalone.ps1" -Path "%STANDALONE_DIR%" >nul 2>nul
exit /b 0

:failed
echo.
echo Frontend build failed.
if "%PM2_AVAILABLE%"=="1" (
    echo Trying to restart existing PM2 app %PM2_APP_NAME%...
    if exist "%STANDALONE_DIR%\server.js" (
        set "PORT=%FRONTEND_PORT%"
        call "%PM2_CMD%" start "%ProgramFiles%\nodejs\node.exe" --name "%PM2_APP_NAME%" --cwd "%STANDALONE_DIR%" -- server.js >nul 2>nul
    )
)
pause
exit /b 1
