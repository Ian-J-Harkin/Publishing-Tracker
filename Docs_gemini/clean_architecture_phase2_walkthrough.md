# Clean Architecture Migration: Phase 2 Walkthrough

This document outlines the strategy for migrating the remaining services (`AuthService`, `CsvImportService`, `ImportBackgroundService`, etc.) to our new Clean Architecture + CQRS paradigm.

---

## 🛑 Lessons Learned & Gotchas from Phase 1

Before diving into the next services, let's review the "battle scars" we picked up while refactoring `BookService`. Keep these top of mind:

### 1. The "Cascading Compiler Error" Illusion
**What Happened:** You got an error saying `var books` didn't exist, but it was actually caused by a namespace mismatch on the `BookResponseDto` inside the `IBookRepository` interface.
**The Lesson:** If a method like `_repository.GetAllAsync(...)` fails to compile because the interface signature doesn't match perfectly, C# will throw a fit and say the resulting variable doesn't exist. Always double-check your interface signatures!

### 2. Namespace Chaos During Folder Restructuring
**What Happened:** Moving `User.cs` to `PublishingTracker.Domain.Entities` broke `ITokenService` because it was still looking in `Api.Models`.
**The Lesson:** Moving files across physical folders (Domain, Application, Infrastructure) changes their namespaces. Use your IDE's "Find and Replace" or "Rename" tools globally to ensure `using` statements are updated everywhere.

### 3. CQRS Rule: The "Same File" DTOs
**What Happened:** We needed a specific `BookResponseDto` for the "Get All Books" query.
**The Lesson:** In CQRS, **do not share DTOs** between different queries if you can avoid it. Define your `Command`/`Query` record and its specific `ResponseDto` record in the **exact same file** as the Handler (but outside the class body). This keeps feature-related code perfectly bundled together.

### 4. Domain-Driven Design (DDD) Entity Protection
**What Happened:** We made `Book.Id` use a `private set;`, which broke the old `IEntity` interface that demanded a public setter.
**The Lesson:** Domain entities should protect their state. Properties should be updated via explicit methods (e.g., `book.UpdatePricing(...)`) rather than public setters. Interfaces must reflect this by only requiring `get;`.

### 5. Read-Only Query Performance Optimization
**What Happened:** In `GetBooksQueryHandler`, we mapped directly to `BookResponseDto` inside the Repository's `.Select()` method.
**The Lesson:** For Queries (fetching data), do not load full Domain Entities (like `Book` or `Sale`) into memory just to map them to DTOs. Do the mapping in the `Select` statement so Entity Framework translates it directly into optimized SQL.

---

## 🗺️ Step-by-Step Instructions for Phase 2

Now that you have the muscle memory from `BookService`, you can apply this exact same formula to the remaining services. Let's use `AuthService` as the next target.

### Step 1: Extract the Core Entities (Domain Layer)
1. Ensure `User` is fully migrated to the `Domain/Entities` folder (already done!).
2. Make sure properties like `PasswordHash` are protected with `private set;` and updated via domain methods if necessary.

### Step 2: Define the Port (Application Layer)
1. Create `Application/Interfaces/IUserRepository.cs`.
2. Define only the methods the application needs (e.g., `Task<User?> GetByEmailAsync(string email);`, `Task AddAsync(User user);`).

### Step 3: Create the Adapter (Infrastructure/Persistence Layer)
1. Create `Persistence/Repositories/UserRepository.cs`.
2. Implement the `IUserRepository` interface, injecting the `PublishingTrackerDbContext`.
3. Register it in `Program.cs` (`builder.Services.AddScoped<IUserRepository, UserRepository>();`).

### Step 4: Dismantle the "Fat Service" into CQRS Handlers
Open `AuthService.cs` and look at its methods. Create a new Handler for each one inside `Application/Auth/Commands` or `Queries`:

*   **RegisterAsync:** Becomes `RegisterUserCommandHandler.cs`
*   **LoginAsync:** Becomes `LoginUserQueryHandler.cs` (or Command, depending on if you log the login event in the DB).

*Example Handler Structure:*
```csharp
// Application/Auth/Commands/RegisterUserCommandHandler.cs
public record RegisterUserCommand(string Email, string Password, string FirstName, string LastName) : IRequest<Result<string>>;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<string>>
{
    private readonly IUserRepository _userRepo;
    // ... inject other stuff

    public async Task<Result<string>> Handle(RegisterUserCommand request, CancellationToken token)
    {
        // 1. Check if user exists via _userRepo
        // 2. Hash password
        // 3. Create Domain Entity
        // 4. Save via _userRepo
        // 5. Return success
    }
}
```

### Step 5: Update the Endpoints
1. Open `Features/Auth/AuthEndpoints.cs`.
2. Remove `[FromServices] IAuthService authService`.
3. Inject `[FromServices] IMediator mediator`.
4. Replace the old service calls with `await mediator.Send(new RegisterUserCommand(...));`.

### Step 6: The Final Purge
1. Delete `AuthService.cs`!
2. Delete `IAuthService.cs`!

### Step 7: Repeat for Remaining Services
Follow this exact same 6-step loop for `CsvImportService` and `ImportBackgroundService`. Note that Background Services are a bit special—they will likely act as the "Trigger" that creates and sends MediatR Commands on a schedule, rather than being triggered by an HTTP Endpoint.
