@echo off
echo Starting Backend API...
start "Backend API" cmd /k "dotnet run --project src/PublishingTracker.Api/PublishingTracker.Api/"

echo Starting Frontend Application...
cd src/publishing-tracker-ui
start "Frontend App" cmd /k "npm run dev"

echo Both servers are starting in separate windows.