# Adversarial Architecture Review: PublishingTracker

**Date:** 2026-08-21  
**Scope:** .NET Backend (Clean Architecture + CQRS) and React Frontend (Hexagonal)  
**Starting Point:** [application_deficiencies_waypoint.md](file:///c:/Github/PublishingTracker/Docs_gemini/application_deficiencies_waypoint.md) and related documentation  
**Method:** Code-level audit against the documented claims and canonical Clean Architecture / Hexagonal Architecture design goals

---

## Executive Verdict

The waypoint document declares *"100% of the critical deficiencies have been addressed"* and the application is *"architecturally sound."* This is an overstatement. The refactoring has achieved genuine structural improvement — the four-project split is real, MediatR CQRS is in place for most features, and the React ports/adapters pattern is functional. However, the migration is **incomplete and internally inconsistent**. Several design goals that Clean Architecture is supposed to guarantee are actively violated in the current codebase. Some of these are minor cosmetic issues; others are load-bearing structural problems that undermine the stated rationale for the refactoring.

Below I grade each area using a traffic light: 🟢 Solid, 🟡 Partial / Flawed, 🔴 Broken / Violated.

---

## Corrected Readout

### Confirmed
- `ImportEndpoints.cs` still mixes HTTP, file I/O, CSV parsing, persistence, and background orchestration.
- DTOs still live in Domain.
- `IApplicationDbContext` still exposes `DbSet<T>` and keeps Application coupled to EF Core.
- `User` and `ImportJob` remain more mutable than the walkthrough implies.
- The frontend still leaks adapter types and Axios-shaped errors into page code.

### Overstated
- Namespace mismatches on endpoint files are a smell, but not a dependency-rule violation by themselves.
- “Import never migrated” is too absolute; there is already an `ImportCsvCommand` and background service, just not a fully clean endpoint boundary.
- “No unit tests for CQRS handlers” is false; there are handler-level tests in `AuthHandlersTests.cs`.
- Package-version drift and migrations living in API are cleanup issues, not core architectural failures.

### Stale / Contradicted by current code
- The testing story is mixed: there are integration tests using LocalDB and unit tests using InMemory, so the docs are not as linear as the review suggests.
- The “17 integration tests” claim does not match the current visible test files/counts.
- `USER_TESTING_CLEAN_ARCH.md` still documents an old import route and an old status vocabulary, so its import section is out of sync with the current code.

## Part 1: .NET Backend — Clean Architecture Conformance

### 1. 🔴 The Dependency Rule Is Violated

The cardinal rule of Clean Architecture: **inner layers must not know about outer layers.** Dependencies point inward only: Domain → (nothing), Application → Domain, Infrastructure → Application + Domain, API → Infrastructure + Application.

#### Finding: Endpoint files live in the API project but declare Application-layer namespaces

Every single endpoint file in [`Endpoints/`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Endpoints) uses an Application-layer namespace:

```
AuthEndpoints.cs        → namespace PublishingTracker.Application.Auth;
BookEndpoints.cs        → namespace PublishingTracker.Application.Books;
SalesEndpoints.cs       → namespace PublishingTracker.Application.Sales;
PlatformEndpoints.cs    → namespace PublishingTracker.Application.Platforms;
ImportEndpoints.cs      → namespace PublishingTracker.Application.Import;
DashboardEndpoints.cs   → namespace PublishingTracker.Application.Dashboard;
UserEndpoints.cs        → namespace PublishingTracker.Application.Users;
```

**Why this matters:** This isn't just cosmetic. These files are *physically* in the Presentation layer (`PublishingTracker.Api`) but *logically* declare themselves as Application-layer members. The compiler treats namespace as the logical identity. This means:

- Any code in `PublishingTracker.Application` can "see" these endpoint classes as if they were siblings — blurring the very boundary the four-project split was designed to enforce.
- IDE tooling (Find All References, namespace-based code maps) will misrepresent the architecture.
- A future developer following namespace conventions will put presentation-layer code in the wrong project.

> **Severity: HIGH.** The entire justification for splitting into four projects was "strict, compiler-level dependency rules that prevent future architectural regressions" (waypoint doc, Item 5). This namespace trick silently defeats that goal.

#### Finding: ImportEndpoints directly references Infrastructure concrete types

[`ImportEndpoints.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Endpoints/ImportEndpoints.cs) has:

```csharp
using PublishingTracker.Infrastructure.Persistence;   // Line 6
using PublishingTracker.Infrastructure.Services;       // Lines 11-12 (duplicated!)
```

And directly injects:
- `PublishingTrackerDbContext db` (lines 23, 81, 126, 143) — the concrete EF Core DbContext
- `ImportBackgroundService backgroundService` (line 82) — the concrete hosted service

**Why this matters:** The presentation layer is supposed to communicate with the application layer through mediator commands/queries, or at most through abstraction interfaces. Directly injecting `PublishingTrackerDbContext` (the EF Core concrete class from Infrastructure) into an endpoint completely bypasses the repository/CQRS pattern that every other endpoint correctly uses. It means:

- The Import endpoints cannot be unit-tested without a real database context.
- The Import feature has zero separation of concerns — it does file I/O, CSV parsing (via CsvHelper directly in the endpoint!), database writes, and background service orchestration all in the same method.
- This is literally the same "complex, inline Entity Framework queries polluting the Presentation layer" that the backend refactoring plan said must be eliminated (and which was done for Dashboard).

> **Severity: CRITICAL.** This is Item 6 of the original deficiency report — *"Missing Infrastructure Components"* — marked as ✅ RESOLVED in the waypoint. The Import endpoints are the worst remaining offender and were never actually migrated to CQRS.

---

### 2. 🔴 DTOs in the Domain Layer

[`PublishingTracker.Domain/Dtos/`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Domain/Dtos) contains 11 DTO files:

| File | Contents |
|------|----------|
| `BookDto.cs` | `BookDto`, `CreateBookDto`, `UpdateBookDto`, `BookPerformanceDto` |
| `SaleDto.cs` | `SaleResponseDto`, `CreateSaleDto`, `SalesSummaryDto` |
| `PlatformDto.cs` | `PlatformResponseDto`, `PlatformRequestDto` |
| `DashboardDto.cs` | Dashboard aggregation DTOs |
| `ImportDto.cs` | Import mapping / preview DTOs |
| `LoginRequest.cs` | `LoginRequest` |
| `RegisterRequest.cs` | `RegisterRequest` |
| `AuthResponse.cs` | `AuthResponse` |
| `UserResponseDto.cs` | `UserResponseDto` |
| `CurrencyTotalDto.cs` | `CurrencyTotalDto` |
| `SalesSummaryDto.cs` | `SalesSummaryDto` |

**Why this matters:** The Domain layer is the innermost ring. It should contain *only* enterprise business rules — entities, value objects, domain events, and domain service interfaces. DTOs are **data transfer contracts** — they exist to shape data for the Application or Presentation layers. Placing them in the Domain layer means:

- The Domain project has outward-facing knowledge about how data is presented to consumers.
- Every time a UI requirement changes a DTO shape, the Domain project must be recompiled and redeployed.
- The Domain becomes a dumping ground — a "shared kernel" anti-pattern where everything references Domain just to get the DTOs, not because they need business rules.

This is especially ironic given that the [clean_architecture_phase2_walkthrough.md](file:///c:/Github/PublishingTracker/Docs_gemini/clean_architecture_phase2_walkthrough.md) (Lesson #3) explicitly states: *"In CQRS, do not share DTOs between different queries… Define your Command/Query record and its specific ResponseDto record in the exact same file as the Handler."* This advice is systematically ignored — the DTOs live in the Domain project, not co-located with their handlers.

> **Severity: HIGH.** This is an architectural smell that will silently accumulate coupling over time.

---

### 3. 🟡 Entity Encapsulation Is Inconsistent

The documentation emphasises DDD-style encapsulation via `private set` and factory methods. Let's check:

| Entity | `private set`? | Factory method? | Notes |
|--------|:---:|:---:|-------|
| [`Sale.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Domain/Entities/Sale.cs) | ✅ All | ✅ `Sale.Create()` | Good |
| [`Platform.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Domain/Entities/Platform.cs) | ✅ All | ✅ `Platform.Create()` | Good |
| [`User.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Domain/Entities/User.cs) | ⚠️ **Mixed** | ✅ `User.Create()` | **`Id`, `Email`, `FirstName`, `LastName`, `CreatedAt`, `UpdatedAt` all have `public set`** |
| [`ImportJob.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Domain/ImportJob.cs) | 🔴 **None** | 🔴 **None** | Fully anemic — all properties have `public set`, no factory method, no invariant enforcement |

The `User` entity is half-encapsulated: `PasswordHash` and `Role` correctly use `private set`, but core identity fields like `Email`, `FirstName`, `LastName`, and even `Id` are fully `public set`. This means any layer can silently mutate a user's email without going through a domain method — exactly the scenario the refactoring was supposed to prevent.

`ImportJob` is a completely "old-style" entity that was never migrated to the DDD pattern. It sits at the Domain root level rather than in `Entities/`, and it uses direct property assignment everywhere (including in [ImportEndpoints.cs line 96-104](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Endpoints/ImportEndpoints.cs#L96-L104) where `new ImportJob { ... }` is used with object initializer syntax).

> **Severity: MEDIUM.** Partial enforcement is arguably worse than no enforcement because it creates a false sense of safety.

---

### 4. 🟡 Application Layer Has an EF Core Dependency

[`PublishingTracker.Application.csproj`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Application/PublishingTracker.Application.csproj) references:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.8" />
```

And [`IApplicationDbContext.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Application/Interfaces/IApplicationDbContext.cs) uses `DbSet<T>`:

```csharp
using Microsoft.EntityFrameworkCore;
...
DbSet<User> Users { get; }
DbSet<Book> Books { get; }
```

**Why this matters:** In strict Clean Architecture, the Application layer should depend *only* on the Domain layer and framework-agnostic abstractions (like MediatR's `IRequest`). By referencing `Microsoft.EntityFrameworkCore` and using `DbSet<T>` in the application interface, you've coupled the Application layer to EF Core's object model. If you ever wanted to swap EF Core for Dapper, or test handlers without EF Core's assemblies, you can't — the interface itself demands EF types.

The orthodox approach is to define repository interfaces returning domain entities or plain collections, which is *also* done via `IBookRepository` and `IUserRepository`. This creates a **dual data-access pattern** — some handlers use the generic `IRepository<T>` + `IApplicationDbContext`, others use specific repositories. This is confusing and inconsistent.

> **Severity: MEDIUM.** This is a common pragmatic shortcut, but it contradicts the Clean Architecture claim.

---

### 5. 🟡 Migrations Live in the Wrong Project

EF Core migrations live in [`PublishingTracker.Api/Migrations/`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Migrations). In a properly structured Clean Architecture solution, migrations belong in the Infrastructure project (alongside the DbContext they describe). Placing them in the API project:

- Forces the API project to reference `Microsoft.EntityFrameworkCore.Design` (which it does)
- Means the API project contains persistence implementation details
- Makes it impossible to run migrations from the Infrastructure project in isolation

> **Severity: LOW-MEDIUM.** Functionally harmless but architecturally untidy.

---

### 6. 🟡 Mixed Error Handling Patterns

The codebase uses three different error-handling strategies simultaneously:

1. **`Result<T>` pattern** (Domain layer) — used by most CQRS handlers
2. **Null checks** — [`AuthEndpoints.cs`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Endpoints/AuthEndpoints.cs) checks `if (authResponse == null)` instead of using `Result<T>.IsSuccess`
3. **Raw exceptions** — Domain entity factory methods throw `ArgumentException` directly, which bubble up unhandled to the global exception handler

The Auth endpoints don't use `Result<T>` at all — the handlers appear to return `AuthResponse?` (nullable) rather than `Result<AuthResponse>`. This is inconsistent with how Sales, Platforms, Books, and Dashboard all work.

> **Severity: MEDIUM.** Inconsistent error handling makes the API unpredictable for consumers.

---

### 7. 🟡 Vestigial / Ghost Directories

The API root contains several suspicious directories:

| Path | Contents |
|------|----------|
| `CSharp PublishingTracker.Api/Features/Platforms/` | A stale "features" folder from a prior refactoring attempt |
| `src/obj/` | An orphaned `obj` folder |
| `output.log` (73KB), `output2.log` (67KB), etc. | Build/debug log files checked into the project root |
| `UnitTest_Investigation_Report.md` + `test_UnitTest_Investigation_Report.md` | Investigation notes in the solution root |
| `Application\NewFolder\` | An empty folder referenced in the API `.csproj` as `<Folder Include="Application\NewFolder\" />` |

> **Severity: LOW** individually but collectively signals incomplete cleanup and rushed refactoring.

---

### 8. 🟡 Package Version Mismatches

The API project references .NET 9.0 (`<TargetFramework>net9.0</TargetFramework>`) but some packages target .NET 8.0:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.17" />
```

While `Microsoft.EntityFrameworkCore` packages are at 9.0.8. This mismatch can cause subtle runtime behavior differences and is a maintenance liability.

Additionally, the API `.csproj` includes both `Microsoft.EntityFrameworkCore.SqlServer` **and** `Npgsql.EntityFrameworkCore.PostgreSQL` — two competing database providers in the same project.

> **Severity: LOW-MEDIUM.** Not a Clean Architecture issue per se, but relevant to "architecturally sound."

---

### 9. 🟢 What's Done Well on the Backend

Credit where it's due:

- **Project-level dependency graph is correct** in `.csproj` references: Domain has no references, Application references only Domain, Infrastructure references Application + Domain, API references Application + Infrastructure. This is the correct dependency flow.
- **CQRS for most features** — Books, Sales, Platforms, Dashboard, Auth, Users all route through MediatR handlers. This is genuine separation.
- **Multi-tenancy via global query filters** in the DbContext is a clean solution.
- **`DependencyInjection.cs` extension methods** in both Application and Infrastructure are a good organizational pattern.
- **`Sale.Create()` factory method** is well-designed with proper invariant validation.
- **Background service using `Channel<T>`** is an appropriate choice for an MVP.

---

## Part 2: React Frontend — Hexagonal Architecture Conformance

### 10. 🟢 Ports & Adapters Structure Is Legitimate

The React frontend genuinely implements a ports-and-adapters pattern:

- **Ports** ([`src/ports/index.ts`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/ui-react/publishing-tracker-ui/src/ports/index.ts)): Clean TypeScript interfaces defining contracts (`IBookPort`, `ISalePort`, `IDashboardPort`, etc.)
- **Adapters** ([`src/adapters/`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/ui-react/publishing-tracker-ui/src/adapters)): Concrete Axios implementations for each port
- **DI Context** ([`DependencyContext.tsx`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/ui-react/publishing-tracker-ui/src/context/DependencyContext.tsx)): React context providing concrete adapters, swappable for testing

This is genuinely useful. Pages like [`ProfilePage.tsx`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/ui-react/publishing-tracker-ui/src/pages/ProfilePage.tsx) correctly use `const { users } = useDependencies();` instead of importing Axios directly.

> **Severity: N/A — this is a genuine strength.**

---

### 11. 🟡 Port Interface Leaks Implementation Detail

[`IAuthPort`](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/ui-react/publishing-tracker-ui/src/ports/index.ts#L8-L11) is weakly typed:

```typescript
export interface IAuthPort {
    register(data: Record<string, unknown>): Promise<any>;
    login(data: Record<string, unknown>): Promise<any>;
}
```

Using `Record<string, unknown>` and `Promise<any>` eliminates the type safety that TypeScript (and ports-and-adapters) is supposed to provide. Every other port interface uses proper typed parameters and return types. The Auth port is the odd one out.

> **Severity: MEDIUM.** Type safety is the whole point of using TypeScript.

---

### 12. 🟡 Import Leaking Through the Port Boundary

The `User` type is imported from the adapter, not from a shared types directory:

```typescript
// ports/index.ts, line 6
import { User } from '../adapters/UserAxiosAdapter'; // TODO: Move User interface to types/user.ts
```

And the same import appears in [`ProfilePage.tsx` line 2](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/ui-react/publishing-tracker-ui/src/pages/ProfilePage.tsx#L2):

```typescript
import { User } from '../adapters/UserAxiosAdapter';
```

The TODO comment has been there since the refactoring was done. A page component is importing a type from a *concrete adapter* — the very coupling the hexagonal pattern was designed to eliminate. If you swapped the Axios adapter for a fetch adapter or a mock, the page would still have a hard dependency on the Axios adapter module.

> **Severity: MEDIUM.** The architecture has a self-documented flaw that was never addressed.

---

### 13. 🟡 No Error Handling Standardisation on the Frontend

Pages catch errors but handle them inconsistently:

```typescript
// ProfilePage.tsx, lines 44-46
} catch (error: any) {
    setProfileMessage({ text: error.response?.data || 'Failed to update profile', type: 'error' });
}
```

Reaching into `error.response?.data` assumes Axios error shape — this is infrastructure knowledge leaking into the page layer. If the adapter implementation changed, every catch block in every page would need updating.

The ports don't define an error contract. A proper hexagonal approach would have the port return a `Result<T>` (or equivalent) rather than throwing, or the adapter would normalize errors before re-throwing.

> **Severity: MEDIUM.** This couples every consumer to Axios's error shape.

---

## Part 3: Cross-Cutting Concerns & Documentation Claims

### 14. 🟡 Test Architecture Claims vs. Reality

The waypoint states *"All 17 integration tests are compiling and passing cleanly."* The documentation includes:

- An integration test file using real SQL Server LocalDB
- A separate test project using `WebApplicationFactory` with mocked repositories

However, the test document ([New Clean Arch Tests.md](file:///c:/Github/PublishingTracker/Docs_gemini/New%20Clean%20Arch%20Tests.md)) still recommends: *"Repository Integration Tests — use EF Core's `UseInMemoryDatabase`."* This contradicts the waypoint's claim that InMemoryDatabase was removed and replaced with LocalDB. The documentation is internally contradictory.

More importantly, there appear to be **no unit tests for the CQRS handlers in isolation** — the tests described are all integration-level. The clean architecture phase 2 walkthrough explicitly says to unit-test handlers by mocking ports, but there's no evidence this was done.

One more same-class mismatch: `USER_TESTING_CLEAN_ARCH.md` describes `POST /api/import/csv` and a `"Pending"` job state, but the current code uses `POST /api/import/upload` + `POST /api/import/process` and returns `"Queued"`/`"Completed"` job states. That makes the import-testing section stale even where the overall intent is still useful.

> **Severity: MEDIUM.** The testing story is less robust than documented.

---

### 15. 🔴 ImportEndpoints Is the Remaining Monolith

This deserves emphasis as a standalone finding. The [Import endpoints file](file:///c:/Github/PublishingTracker/Publishing-Tracker/apps/api/PublishingTracker.Api/PublishingTracker.Api/Endpoints/ImportEndpoints.cs) is 161 lines of endpoint code that:

1. **Directly accesses `PublishingTrackerDbContext`** (not through a repository or mediator)
2. **Performs CSV parsing inline** using `CsvHelper` (lines 47-68) — this is infrastructure/parsing logic in the presentation layer
3. **Does file I/O** (`File.Exists`, `FileStream`) in the endpoint handler
4. **Creates domain entities via object initializer** (`new ImportJob { ... }`) bypassing the factory method pattern
5. **Directly calls `ImportBackgroundService.EnqueueAsync()`** by injecting the concrete class
6. **Has duplicate using statements** (`using PublishingTracker.Domain.Dtos;` appears twice; `using PublishingTracker.Infrastructure.Services;` appears twice)

This single file violates essentially every principle the refactoring was meant to establish. It is the pre-refactoring architecture preserved intact within the "Clean Architecture" solution.

> **Severity: CRITICAL.** The waypoint says "100% of critical deficiencies addressed." The Import feature was never actually migrated.

---

## Summary Scorecard

| # | Area | Grade | Key Issue |
|---|------|:-----:|-----------|
| 1 | Dependency Rule (namespaces) | 🔴 | All endpoints use Application-layer namespaces |
| 2 | DTOs in Domain | 🔴 | 11 DTO files in innermost ring |
| 3 | Entity encapsulation | 🟡 | User + ImportJob not fully encapsulated |
| 4 | Application ↔ EF Core coupling | 🟡 | `DbSet<T>` in Application interfaces |
| 5 | Migration placement | 🟡 | Migrations in API, not Infrastructure |
| 6 | Error handling | 🟡 | Three competing patterns |
| 7 | Cleanup / hygiene | 🟡 | Ghost dirs, log files, empty folders |
| 8 | Package versions | 🟡 | .NET 8 packages on .NET 9 runtime |
| 9 | CQRS adoption | 🟢 | Most features properly migrated |
| 10 | React ports/adapters | 🟢 | Genuine hexagonal implementation |
| 11 | Auth port typing | 🟡 | `Record<string, unknown>` / `any` |
| 12 | User type import leak | 🟡 | Self-documented TODO never addressed |
| 13 | Frontend error handling | 🟡 | Axios shape assumed in pages |
| 14 | Test claims vs. reality | 🟡 | Contradictory documentation |
| 15 | ImportEndpoints monolith | 🔴 | Never migrated to CQRS |

**Overall: 3 Critical/High (🔴), 9 Medium (🟡), 2 Good (🟢)**

---

## Recommendations (Priority-Ordered)

### Must-Fix (Architecture is actively undermined)

1. **Migrate ImportEndpoints to CQRS.** Create `UploadCsvCommand`, `ProcessImportCommand`, `GetImportStatusQuery`, `GetImportHistoryQuery` handlers. Remove all direct `DbContext` and `CsvHelper` references from the endpoint file.

2. **Move DTOs out of Domain.** Either co-locate them with their handlers (as the phase 2 walkthrough recommends) or create a `PublishingTracker.Application.Contracts` shared project. The Domain layer should contain only entities, value objects, and domain-service interfaces.

3. **Fix endpoint namespaces.** Every file in `PublishingTracker.Api/Endpoints/` should use `namespace PublishingTracker.Api.Endpoints;` — matching their physical location and logical layer.

### Should-Fix (Inconsistencies that erode trust)

4. **Complete User entity encapsulation.** Make `Id`, `Email`, `FirstName`, `LastName`, `CreatedAt`, `UpdatedAt` use `private set`. Add mutation methods like `UpdateEmail(string newEmail)`.

5. **Migrate `ImportJob` to DDD pattern.** Add `private set`, factory method, move to `Entities/` folder.

6. **Move `User` type to `types/user.ts`** in the React frontend. The TODO has been sitting there since the refactoring.

7. **Standardise error handling.** Have Auth endpoints return `Result<T>` like everything else. Create an error normalization layer in the React adapters.

### Nice-to-Fix (Cleanup)

8. Move migrations to Infrastructure project.
9. Remove `IApplicationDbContext` or remove per-entity repositories — pick one data-access strategy.
10. Delete ghost directories, log files, empty `NewFolder`, and stale investigation reports.
11. Align NuGet package versions to .NET 9.0.
12. Type the `IAuthPort` interface properly.
