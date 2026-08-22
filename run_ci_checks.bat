@echo off
setlocal
cd /d "%~dp0"

set "CANONICAL_SCRIPT=%~dp0Publishing-Tracker\run_ci_checks.bat"
if not exist "%CANONICAL_SCRIPT%" (
    echo [ERROR] Canonical CI script not found: "%CANONICAL_SCRIPT%"
    exit /b 1
)

call "%CANONICAL_SCRIPT%"
exit /b %ERRORLEVEL%