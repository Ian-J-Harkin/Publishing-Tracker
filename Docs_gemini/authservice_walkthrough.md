# AuthService Migration – Detailed Walkthrough

This guide walks you step‑by‑step through converting the legacy **AuthService** (and its interface `IAuthService`) into a Clean Architecture + CQRS design. It follows the same pattern we used for `BookService` so you can reuse the mental model.

---

## 📦 1. Project Layout Recap
```
src/
│   PublishingTracker.Api
│   ├─ Application
│   │   ├─ Auth
│   │   │   ├─ Commands   (Register, Refresh, Logout)
│   │   │   └─ Queries    (Login, ValidateToken)
│   │   └─ Interfaces   (IUserRepository, IJwtGenerator)
│   ├─ Domain
│   │   ├─ Entities
│   │   │   └─ User.cs
│   │   └─ Dtos        (UserDto, RegisterDto, LoginDto)
│   ├─ Infrastructure
│   │   └─ Persistence
│   │       └─ Repositories
│   │           └─ UserRepository.cs
│   └─ Api
│       └─ Endpoints
│           └─ AuthEndpoints.cs
```

The **Auth** feature now lives entirely inside its own folder under `Application/Auth`. All related code—records, handlers, DTOs—are co‑located.

---

## 🔍 2. Identify the Existing “Fat” Service
### Current `AuthService.cs`
```csharp
public class AuthService : IAuthService
{
    private readonly PublishingTrackerDbContext _context;
    private readonly ITokenService _tokenService;
    // Register, Login, Refresh, etc.
}
```
It directly accesses EF Core, does password hashing, creates JWTs, and returns raw `Result<T>` objects.

### Why it must go away
* Violates **Dependency‑Inversion** – higher‑level application code depends on a concrete service.
* Couples **authentication logic** with **data access** – hard to test in isolation.
* Prevents **pipeline extensibility** (e.g., adding MFA) because everything lives in one class.

---

## 🛠️ 3. Create the Domain Entity (if not already)
File: `Domain/Entities/User.cs`
```csharp
namespace PublishingTracker.Domain.Entities;

public class User : IEntity
{
    public int Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Factory / invariant enforcement
    public static User Create(string email, string passwordHash, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email required", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash required", nameof(passwordHash));
        return new User { Email = email, PasswordHash = passwordHash, FirstName = firstName, LastName = lastName, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    }

    // Private ctor for EF Core
    private User() { }
}
```
*Note:* The `IEntity` interface now only requires a getter for `Id` (private setter already present).

---

## 📡 4. Define the **Port** (Application‑level interface)
File: `Application/Interfaces/IUserRepository.cs`
```csharp
using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
}
```
Only the operations the **Auth** feature needs are exposed – no CRUD extras.

---

## 🧩 5. Implement the **Adapter** (Infrastructure)
File: `Infrastructure/Persistence/Repositories/UserRepository.cs`
```csharp
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Data; // DbContext lives here

namespace PublishingTracker.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PublishingTrackerDbContext _ctx;
    public UserRepository(PublishingTrackerDbContext ctx) => _ctx = ctx;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        await _ctx.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
        await _ctx.Users.FindAsync(new object[] { id }, ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await _ctx.Users.AddAsync(user, ct);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        _ctx.Users.Update(user);
        await _ctx.SaveChangesAsync(ct);
    }
}
```
Register it in DI (see step 7).

---

## 🗝️ 6. Create a **JWT Generator** abstraction (so Auth logic isn’t tied to a specific library)
File: `Application/Interfaces/IJwtGenerator.cs`
```csharp
namespace PublishingTracker.Application.Interfaces;

public interface IJwtGenerator
{
    string CreateToken(User user);
    // Optional: Validate/Parse token if you want to move that out of the controller as well.
}
```
Implementation lives in `Infrastructure/Services/JwtGenerator.cs` (thin wrapper around the existing `TokenService`).

---

## 📦 7. Register Services in **Program.cs**
Add the following lines (order does not matter but keep them together with other repository registrations):
```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJwtGenerator, JwtGenerator>(); // wrapper around TokenService
```
If you keep `TokenService` as the concrete class, you can simply register it as `IJwtGenerator`.

---

## 🚀 8. Build **CQRS Handlers**
### 8.1 Register (Command)
**File:** `Application/Auth/Commands/RegisterUserCommand.cs`
```csharp
public record RegisterUserCommand(string Email, string Password, string FirstName, string LastName)
    : IRequest<Result<string>>; // returns the JWT on success
```
**Handler:** `RegisterUserCommandHandler.cs`
```csharp
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<string>>
{
    private readonly IUserRepository _userRepo;
    private readonly IJwtGenerator _jwt;
    private readonly IPasswordHasher _hasher; // you can use ASP.NET Core IPasswordHasher<User>

    public RegisterUserCommandHandler(IUserRepository userRepo, IJwtGenerator jwt, IPasswordHasher<User> hasher)
    {
        _userRepo = userRepo; _jwt = jwt; _hasher = hasher;
    }

    public async Task<Result<string>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        // 1️⃣ Ensure email is unique
        var existing = await _userRepo.GetByEmailAsync(request.Email, ct);
        if (existing != null)
            return Result<string>.Failure("Email already registered");

        // 2️⃣ Hash password (bcrypt, PBKDF2, etc.)
        var dummy = new User(); // for hasher generic type – we never persist this instance
        var passwordHash = _hasher.HashPassword(dummy, request.Password);

        // 3️⃣ Create domain entity via factory (enforces invariants)
        var user = User.Create(request.Email, passwordHash, request.FirstName, request.LastName);

        // 4️⃣ Persist
        await _userRepo.AddAsync(user, ct);

        // 5️⃣ Issue JWT
        var token = _jwt.CreateToken(user);
        return Result<string>.Success(token);
    }
}
```
### 8.2 Login (Query – returns token)
**File:** `Application/Auth/Queries/LoginUserQuery.cs`
```csharp
public record LoginUserQuery(string Email, string Password) : IRequest<Result<string>>;
```
**Handler:** `LoginUserQueryHandler.cs`
```csharp
public class LoginUserQueryHandler : IRequestHandler<LoginUserQuery, Result<string>>
{
    private readonly IUserRepository _repo;
    private readonly IJwtGenerator _jwt;
    private readonly IPasswordHasher<User> _hasher;

    public LoginUserQueryHandler(IUserRepository repo, IJwtGenerator jwt, IPasswordHasher<User> hasher)
    {
        _repo = repo; _jwt = jwt; _hasher = hasher;
    }

    public async Task<Result<string>> Handle(LoginUserQuery request, CancellationToken ct)
    {
        var user = await _repo.GetByEmailAsync(request.Email, ct);
        if (user == null)
            return Result<string>.Failure("Invalid credentials");

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification != PasswordVerificationResult.Success)
            return Result<string>.Failure("Invalid credentials");

        var token = _jwt.CreateToken(user);
        return Result<string>.Success(token);
    }
}
```
### 8.3 (Optional) Refresh Token Command & Logout Command
Follow the same pattern – create a `RefreshTokenCommand` and a `LogoutCommand` that manipulate a possible `RefreshToken` entity stored in the DB.

---

## 📡 9. Adjust **Endpoints**
Open `Features/Auth/AuthEndpoints.cs` (or create it if missing) and replace the old service calls with MediatR:
```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (
            RegisterUserDto dto,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var cmd = new RegisterUserCommand(dto.Email, dto.Password, dto.FirstName, dto.LastName);
            var result = await mediator.Send(cmd, ct);
            return result.ToActionResult(); // extension that converts Result<T> to IActionResult
        });

        app.MapPost("/api/auth/login", async (
            LoginDto dto,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new LoginUserQuery(dto.Email, dto.Password);
            var result = await mediator.Send(query, ct);
            return result.ToActionResult();
        });
    }
}
```
*All DTOs (`RegisterUserDto`, `LoginDto`) are simple request models that live next to the endpoint file.*

---

## ✅ 10. Verify & Test
1. **Build** – the solution should compile with **zero** references to `IAuthService` or `AuthService`. 
2. **Run the API** and hit `/api/auth/register` and `/api/auth/login` using Swagger/Postman. Verify a JWT string is returned.
3. **Unit test** the handlers by mocking `IUserRepository`, `IJwtGenerator`, and `IPasswordHasher`. No EF Core needed for pure business‑logic tests.
4. **Integration test** the endpoint using `WebApplicationFactory` – it will hit the real `PublishingTrackerDbContext` (in‑memory DB works fine).

---

## ⚠️ Gotchas & Lessons Learned (specific to Auth)
| Issue | Why it happened | Fix / Prevention |
|------|----------------|-------------------|
| **Password hashing hidden in service** | `AuthService` did the hash inline, making it hard to replace algorithm. | Extract `IPasswordHasher<User>` from ASP.NET Core; inject it into handlers. |
| **JWT creation tightly coupled to `TokenService`** | Direct `new TokenService(...)` call broke DI. | Introduce `IJwtGenerator` abstraction; the concrete class simply delegates to the existing `TokenService`. |
| **Endpoint still referenced `IAuthService`** | When refactoring, the old `AuthEndpoints.cs` wasn’t updated. | Search for `IAuthService` after the migration; replace each usage with `IMediator`. |
| **Duplicate DTO names** (e.g., `LoginDto` in both `Features/Auth` and `Application/Auth`) | Copy‑pasting without namespace hygiene. | Keep DTOs **next to their handler** and give them a clear `record` name (e.g., `RegisterUserDto`). |
| **Missing registration of the new repository** | Forgetting `builder.Services.AddScoped<IUserRepository, UserRepository>()` caused runtime DI errors. | Verify DI registration by building and checking the service provider in a quick console test. |

---

## 📚 11. Summary Checklist (copy‑paste into your README if you wish)
- [ ] **Domain**: `User` entity with factory method.
- [ ] **Application**: `IUserRepository`, `IJwtGenerator` interfaces.
- [ ] **Infrastructure**: `UserRepository`, `JwtGenerator` (wrapper around existing `TokenService`).
- [ ] **CQRS**: `RegisterUserCommand/Handler`, `LoginUserQuery/Handler` (and any optional refresh/logout commands).
- [ ] **Endpoints**: `AuthEndpoints.cs` using `IMediator` only.
- [ ] **DI**: Register all new services in `Program.cs`.
- [ ] **Tests**: Unit‑test handlers, integration‑test endpoints.
- [ ] **Cleanup**: Delete `AuthService.cs` and `IAuthService.cs` after all endpoints compile.

When you tick every box, the **Auth** feature is fully migrated to Clean Architecture and ready for further extensions (MFA, social login, etc.) without ever touching the domain or infrastructure layers again.

---

*Happy refactoring!* 🎉
