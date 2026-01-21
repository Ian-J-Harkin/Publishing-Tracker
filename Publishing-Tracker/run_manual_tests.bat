@echo off
setlocal enabledelayedexpansion

:: Change the working directory to the location of this script
cd /d "%~dp0"

echo Current directory fixed to: %cd%
echo.

:: Define paths relative to the script location
set "API_PATH=%~dp0apps\api\PublishingTracker.Api\PublishingTracker.Api"
set "UI_PATH=%~dp0apps\ui-react"

echo API path: %API_PATH%
echo React path: %UI_PATH%
echo.

echo Starting Backend API...
:: We use /D to set the starting directory and 'pushd' inside the command for redundancy
start "Backend API" /D "%API_PATH%" cmd /k "dotnet run"

echo Starting Frontend Application...
start "Frontend App" /D "%UI_PATH%" cmd /k "npm run dev"

echo Both servers are starting in separate windows.
pause