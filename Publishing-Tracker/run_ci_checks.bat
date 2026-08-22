@echo off
setlocal
cd /d "%~dp0"

echo Running monorepo CI checks...
echo.

echo [1/6] Restoring API solution...
dotnet restore apps\api\PublishingTracker.Api.sln
if errorlevel 1 goto :fail

echo [2/6] Building API solution...
dotnet build apps\api\PublishingTracker.Api.sln --configuration Release --no-restore
if errorlevel 1 goto :fail

echo [3/6] Testing API solution...
dotnet test apps\api\PublishingTracker.Api.sln --no-build --verbosity normal
if errorlevel 1 goto :fail

echo [4/6] Installing frontend dependencies...
npm ci
if errorlevel 1 goto :fail

echo [5/6] Building React workspace...
npm run build --workspace=@publishing-tracker/ui-react
if errorlevel 1 goto :fail

echo [6/6] Building Angular workspace...
npm run build --workspace=@publishing-tracker/ui-angular
if errorlevel 1 goto :fail

echo.
echo CI checks complete.
exit /b 0

:fail
echo.
echo CI checks failed.
exit /b 1
