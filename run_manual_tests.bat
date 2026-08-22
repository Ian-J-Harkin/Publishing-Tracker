@echo off
setlocal
cd /d "%~dp0"

set "CANONICAL_SCRIPT=%~dp0Publishing-Tracker\run_manual_tests.bat"
if not exist "%CANONICAL_SCRIPT%" (
    echo [ERROR] Canonical service-runner script not found: "%CANONICAL_SCRIPT%"
    exit /b 1
)

call "%CANONICAL_SCRIPT%"
exit /b %ERRORLEVEL%