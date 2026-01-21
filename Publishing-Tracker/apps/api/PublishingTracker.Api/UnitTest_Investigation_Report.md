# Unit Test Investigation Report

### **Objective:**
The initial request was to run the "swagger tests" to understand how to fix the existing unit tests for the `PublishingTracker.Api` project.

### **Investigation Steps & Findings:**

1.  **Initial Exploration:**
    *   **Action:** Listed files in the `PublishingTracker.Api` and `PublishingTracker.Api/PublishingTracker.Api.Tests` directories.
    *   **Findings:** Identified a .NET solution with a main API project (`PublishingTracker.Api`) and a test project (`PublishingTracker.Api.Tests`). The test project uses `WebApplicationFactory` (via `TestWebAppFactory.cs`), which is standard for integration testing in ASP.NET Core. No "swagger tests" were found, so the investigation shifted to fixing the existing unit/integration tests.

2.  **Configuration Analysis:**
    *   **Action:** Inspected the contents of `Program.cs` (the application's entry point) and `TestWebAppFactory.cs` (the test setup).
    *   **Findings:**
        *   `Program.cs` configures the application to use a SQL Server database (`UseSqlServer`) in non-testing environments.
        *   `TestWebAppFactory.cs` configures the test environment to use an in-memory database (`UseInMemoryDatabase`) for test isolation.

3.  **Initial Test Execution:**
    *   **Action:** Executed the tests using the command `dotnet test PublishingTracker.Api/PublishingTracker.Api.Tests/PublishingTracker.Api.Tests.csproj`.
    *   **Findings:** The tests failed with two primary, related exceptions:
        1.  `System.InvalidOperationException: Services for database providers 'Microsoft.EntityFrameworkCore.SqlServer', 'Microsoft.EntityFrameworkCore.InMemory' have been registered in the service provider. Only a single database provider can be registered...`
        2.  `System.InvalidOperationException: Unable to resolve service for type 'PublishingTracker.Api.Data.PublishingTrackerDbContext' while attempting to activate 'PublishingTracker.Api.Services.AuthService'.`

### **Root Cause Analysis:**

The core problem is a dependency injection conflict. Both the main application's startup configuration (`Program.cs`) and the test-specific configuration (`TestWebAppFactory.cs`) are attempting to register a `DbContext`.

*   The `WebApplicationFactory` starts the application's host, which runs the service registration logic in `Program.cs`, adding the SQL Server `DbContext`.
*   The `ConfigureWebHost` method in `TestWebAppFactory.cs` then runs and attempts to add the in-memory `DbContext`.
*   This results in two `DbContext` providers being registered in the same service container, which Entity Framework Core does not allow, causing the first exception. The subsequent service resolution failures are a direct consequence of this initial conflict.

### **Proposed Solution:**

The standard and correct way to resolve this is to ensure the test configuration overrides the production configuration cleanly. The logic inside `TestWebAppFactory.cs` must first **remove** the `DbContext` registration added by `Program.cs` before **adding** its own in-memory version.

The following change to `PublishingTracker.Api/PublishingTracker.Api.Tests/TestWebAppFactory.cs` will accomplish this:

```csharp
// In PublishingTracker.Api/PublishingTracker.Api.Tests/TestWebAppFactory.cs

protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        // Find and remove the DbContextOptions registration from the main application
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == 
                typeof(DbContextOptions<PublishingTrackerDbContext>));

        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        // Add the in-memory database context for testing
        services.AddDbContext<PublishingTrackerDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestingDb");
        });

        // The rest of the method remains the same...
    });
}
```

This approach isolates the test environment's database configuration from the production configuration, which should resolve all the dependency injection errors and allow the tests to run successfully.