# Entity Migration & Encapsulation Walkthrough

This document provides a detailed breakdown of the changes required to migrate the entities and enforce their invariants via factory methods. Since the "all-or-nothing" approach was too disruptive, this walkthrough explains exactly

### Phase 2: Complete Clean Architecture Migration

Building upon the previous work, the application has now fully migrated towards a Clean Architecture design pattern and Domain-Driven Design (DDD):

#### 1. Entity Encapsulation
- **`Platform.cs` & `Sale.cs`**:
  - Implemented `private set` for properties.
  - Added parameterless private constructors for Entity Framework Core.
  - Introduced `Create(...)` factory methods to enforce domain invariants.
  - Adapted calling services (`PlatformEndpoints.cs`, `SalesEndpoints.cs`, `CsvImportService.cs`, `ImportBackgroundService.cs`) to use these new factory methods.

#### 2. Authentication CQRS Migration
- **Created MediatR Handlers**:
  - Introduced `RegisterUserCommand.cs` and `RegisterUserCommandHandler`.
  - Introduced `LoginUserQuery.cs` and `LoginUserQueryHandler`.
- **Refactored Endpoints**:
  - Updated `AuthEndpoints.cs` to leverage `IMediator` instead of injecting services directly.
- **Removed Legacy Service**:
  - Deleted `AuthService.cs` and `IAuthService.cs` from the project.
  - Refactored `AuthServiceTests.cs` to test the newly created CQRS handlers and renamed the test file to `AuthHandlersTests.cs`.

#### 3. Verification & Fixes
- Re-enabled `ICurrentUserService` in `Program.cs` which had previously broken integration tests.
- Addressed bugs with the newly encapsulated properties inside `CsvImportService` and `ImportBackgroundService` during CSV processing.
- Verified that the solution fully builds using `dotnet build`.
- Ran the comprehensive automated test suite with `dotnet test`, resulting in 100% test passing rate.

## 1. Entity Encapsulation (`Domain/Entities/`)

The core of this change is to protect the internal state of our domain entities. By changing `public set;` to `private set;`, we prevent other layers (like the Application or Presentation layers) from directly modifying an entity's state, which violates Domain-Driven Design (DDD) principles.

Instead, we introduce `static Create(...)` factory methods. These methods act as the only entry point for creating new entities, guaranteeing that any invariants (e.g., "a user must have a valid email", "a sale must have a positive quantity") are enforced before the object is ever created.

### `User.cs`
- **Change**: All properties (`Email`, `FirstName`, `LastName`, etc.) become `private set;`.
- **Factory**: `User.Create(string email, string passwordHash, string firstName, string lastName)` is used to ensure no user is created without essential data.
- **EF Core**: A `private User() { }` constructor is added so Entity Framework Core can still materialize the object from the database using reflection.

### `Book.cs`
- **Change**: Properties like `Title`, `Author`, etc. become `private set;`.
- **Factory**: `Book.Create(int userId, string title, string author)` replaces the public constructor.

### `Platform.cs` & `Sale.cs`
- **Change**: Moved to `Domain/Entities/` (if not already fully tracked there) and properties set to `private set;`.
- **Factory**: `Platform.Create(...)` and `Sale.Create(...)` are introduced to validate business rules (e.g., `CommissionRate >= 0`, `Quantity > 0`).

---

## 2. Refactoring Application Layer (Commands & Services)

Because the entity properties are now protected, any code that previously used object initialization (e.g., `new User { Email = "..." }`) will fail to compile. This necessitates refactoring across the Application layer.

### `AuthService.cs`
- **Previous**: `var user = new User { Email = request.Email, ... };`
- **New**: `var user = User.Create(request.Email, hash, request.FirstName, request.LastName);`

### `CreateBookCommandHandler.cs`
- **Previous**: `var book = new Book(userId, request.Title, request.Author);`
- **New**: `var book = Book.Create(userId, request.Title, request.Author);`

### `PlatformEndpoints.cs` & `SalesEndpoints.cs`
- We must update the API endpoints where new Platforms and Sales are created to use their respective `Create(...)` factory methods rather than raw object initialization.
- **Namespaces**: The `using PublishingTracker.Api.Domain.Entities;` namespace must be added to these files so they can locate the migrated entities.

---

## 3. Refactoring Background Services

### `ImportBackgroundService.cs` & `CsvImportService.cs`
- These services dynamically create `Book`, `Platform`, and `Sale` objects from CSV data. 
- They must be updated to use the factory methods (e.g., `Sale.Create(...)`). Any data parsing (like converting a string quantity to an integer) must happen *before* calling the factory method, ensuring the factory only receives strongly-typed, valid data.

---

## 4. Refactoring the Tests (`.Tests` Project)

Tests often rely heavily on object initialization to quickly setup mock data. 

### `TestWebAppFactory.cs` & `AuthServiceTests.cs`
- Any setup code that seeds the in-memory database with `new User { ... }` must be updated to `User.Create(...)`.
- We must ensure the `.Tests` project references the correct updated namespaces for the entities.

---

## Summary

The cascade of compilation errors (like the ones we saw with `JwtGenerator` and `Results.NotImplemented`) are a natural side-effect of tightening the domain model. By enforcing encapsulation, the compiler immediately flags every place in the codebase that was bypassing our business rules.

**How would you like to proceed?**
We can apply these changes step-by-step (e.g., one entity at a time, followed by its dependent services), or we can tackle specific layers one by one.
