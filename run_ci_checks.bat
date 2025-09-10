@echo off
echo Running .NET Core CI Checks...

echo.
echo Step 1: Restoring dependencies...
dotnet restore

echo.
echo Step 2: Building the project...
dotnet build --configuration Release --no-restore

echo.
echo Step 3: Running tests...
dotnet test --no-restore --verbosity normal

echo.
echo CI checks complete.