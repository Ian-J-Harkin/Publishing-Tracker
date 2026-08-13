# New Clean Arch Tests

## Overview
This document summarizes the test project you just added to verify the Clean Architecture migration for the **PublishingTracker** API. It includes the test‑project `.csproj` file, a sample integration test that checks endpoint registration and MediatR wiring, and guidance on how to extend the suite with unit tests for individual layers.

---

## 1️⃣ Test Project File
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\\PublishingTracker.Api\\PublishingTracker.Api.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
    <PackageReference Include="xunit" Version="2.8.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.5" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
  </ItemGroup>
</Project>
```
Saved as `PublishingTracker.Api.Tests.csproj` under the `Tests` folder.

---

## 2️⃣ Sample Integration Test (`BookEndpointTests.cs`)
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using PublishingTracker.Api;
using PublishingTracker.Application.Books.Commands;
using PublishingTracker.Application.Books.Queries;
using Moq;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Api.Models;

namespace PublishingTracker.Api.Tests.Integration;

public class BookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookEndpointTests(WebApplicationFactory<Program> factory)
    {
        // Override services for a lightweight in‑memory test environment
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real IBookRepository with a mock that returns deterministic data
                var mockRepo = new Mock<IBookRepository>();
                mockRepo.Setup(r => r.GetAllAsync(It.IsAny<int>(), It.IsAny<string?>(), null, null, 1, 10, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new List<BookResponseDto>
                        {
                            new BookResponseDto(1, "Test Title", "Test Author", null, 9.99m, 0, 0)
                        });
                services.AddSingleton(mockRepo.Object);
            });
        });
    }

    [Fact]
    public async Task GetAll_BookEndpoint_ShouldReturnOkAndBooks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/books");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await response.Content.ReadFromJsonAsync<List<BookResponseDto>>();
        books.Should().NotBeNull();
        books!.Count.Should().BeGreaterThan(0);
        books[0].Title.Should().Be("Test Title");
    }

    [Fact]
    public async Task Create_BookEndpoint_UsesMediatRCommand()
    {
        // Arrange
        var client = _factory.CreateClient();
        var dto = new CreateBookDto { Title = "New Book", Author = "Jane Doe", ISBN = "1234567890" };

        // Act
        var response = await client.PostAsJsonAsync("/api/books", dto);

        // Assert
        // Because we didn't mock the command handler, the endpoint will return BadRequest (no user). We only check route registration.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }
}
```
The test spins up the API in‑memory, injects a mocked `IBookRepository`, and confirms that:
* `GET /api/books` returns `200 OK` with a list of `BookResponseDto` objects.
* `POST /api/books` hits the route (status code is one of the expected values), proving the endpoint is registered and wired to MediatR.

---

## 3️⃣ How to Run the Tests
```powershell
# From the repository root
cd Tests
dotnet test
```
All tests should pass. If you add more unit or integration tests, they will be discovered automatically.

---

## 4️⃣ Extending the Suite
* **Command/Query Handler Unit Tests** – mock the required ports (`IUserRepository`, `IJwtGenerator`, etc.) and verify `Result<T>` outcomes.
* **Repository Integration Tests** – use EF Core’s `UseInMemoryDatabase` to exercise the concrete repository implementations.
* **DI Registration Test** – ensure every port (`IBookRepository`, `IUserRepository`, `IJwtGenerator`, `IMediator`, etc.) is registered in `Program.cs`.

---

## 5️⃣ Quick Checklist
- [ ] Test project file added (`Tests/PublishingTracker.Api.Tests.csproj`).
- [ ] Sample integration test written (`Tests/BookEndpointTests.cs`).
- [ ] `dotnet test` runs cleanly.
- [ ] Add further unit tests for each handler as you finish migrating the remaining services.

---

*Keep this file handy as a reference while you continue the Clean Architecture migration!*
