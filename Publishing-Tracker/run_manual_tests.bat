@echo off
setlocal enabledelayedexpansion

:: Change the working directory to the location of this script
cd /d "%~dp0"

echo ==============================================
echo   Publishing Tracker - Manual Service Runner
echo ==============================================
echo.

:: Define paths relative to the script location
set "API_PATH=%~dp0apps\api\PublishingTracker.Api\PublishingTracker.Api"
set "REACT_PATH=%~dp0apps\ui-react\publishing-tracker-ui"
set "ANGULAR_PATH=%~dp0apps\ui-angular\publishing-tracker-ui-angular"

echo [1/3] Starting Backend API...
start "Backend API" /D "%API_PATH%" cmd /k "echo Starting .NET API... && dotnet run"

echo [2/3] Starting React UI...
start "React UI" /D "%REACT_PATH%" cmd /k "echo Starting React... && npm run dev"

echo [3/3] Starting Angular UI...
start "Angular UI" /D "%ANGULAR_PATH%" cmd /k "echo Starting Angular... && npm start"

echo.
echo All services are launching in separate windows.
echo ----------------------------------------------
echo API:     https://localhost:7071 (or as configured)
echo React:   http://localhost:5173
echo Angular: http://localhost:4200
echo ----------------------------------------------
echo.
pause