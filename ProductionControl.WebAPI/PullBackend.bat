@echo off
setlocal

set "REPO_ROOT=%~dp0.."

echo Pulling latest backend source...
cd /d "%REPO_ROOT%"
if errorlevel 1 goto failed

git pull --ff-only
if errorlevel 1 goto failed

echo.
echo Backend pull completed.
pause
exit /b 0

:failed
echo.
echo Backend pull failed.
pause
exit /b 1
