@echo off
setlocal
echo ==============================================
echo   Publishing Tracker - Local CI Simulator
echo ==============================================

echo.
echo [1/4] Checking .NET API...
echo ----------------------------------------------
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api.sln --verbosity minimal
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] API Tests Failed!
) else (
    echo [SUCCESS] API Tests Passed.
)

echo.
echo [2/4] Checking React Frontend...
echo ----------------------------------------------
cd Publishing-Tracker
call npm run build --workspace=@publishing-tracker/ui-react
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] React Build Failed!
) else (
    echo [SUCCESS] React Build Passed.
)
call npm test --workspace=@publishing-tracker/ui-react -- --watchAll=false
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] React Tests Failed!
) else (
    echo [SUCCESS] React Tests Passed.
)
cd ..

echo.
echo [3/4] Checking Angular Frontend...
echo ----------------------------------------------
cd Publishing-Tracker
call npm run build --workspace=@publishing-tracker/ui-angular
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Angular Build Failed!
) else (
    echo [SUCCESS] Angular Build Passed.
)
call npm run test --workspace=@publishing-tracker/ui-angular -- --watch=false --browsers=ChromeHeadless
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Angular Tests Failed!
) else (
    echo [SUCCESS] Angular Tests Passed.
)
cd ..

echo.
echo [4/4] Linting Check...
echo ----------------------------------------------
cd Publishing-Tracker
call npm run lint --workspaces --if-present
cd ..

echo.
echo ==============================================
echo   CI Checks Complete.
echo ==============================================
pause