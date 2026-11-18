# Phase 0 — Expanded Design: Baseline, `.NET 10` Upgrade, Auth Hygiene, and Contracts

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md) and [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md).

---

## 1. Purpose, Scope, and Boundaries

### Goal
Bring the existing `Publishing-Tracker/apps/api` solution to a clean, compiling, green-tested **`.NET 10 LTS` + PostgreSQL** baseline, fix auth/tenant-isolation hygiene gaps, and lock the contracts for Phase 1 (`find_titles_missing_metadata` and `get_title_details`).

### In Scope
1. Resolving existing uncommitted compiler errors and duplicate scratch files across Domain, Application, and Infrastructure.
2. Upgrading `Directory.Build.props`, NuGet packages (`Directory.Packages.props`), and `.github/workflows/api.yml` from `.NET 9` (EOL Nov 10, 2026) to **`.NET 10 LTS` (`net10.0`)**.
3. Standardizing `Program.cs`, EF Core migrations, `TestWebAppFactory.cs`, and CI on **PostgreSQL** (Docker locally/CI, external Neon in production).
4. Fixing `TokenService.cs` (`iat` claim + `Jwt:ExpiryMinutes`) and tightening EF query filters/explicit owner checks.
5. Locking the `FormatCompletenessRules` spec, Phase 1 tool/API DTO contracts, external LLM data-handling policy, and a two-user deterministic seed dataset.

### Out of Scope for Phase 0
- No MCP SDK (`ModelContextProtocol.AspNetCore`) or `/mcp` endpoint wiring yet (Phase 1).
- No `PublishingTracker.Agent` host, `IChatClient` loop, or OpenRouter calls yet (Phase 2).
- No `pgvector` schema or RAG ingestion yet (Phase 4).

---

## 2. Working-Tree Audit & Exact Issues to Fix

Building `PublishingTracker.Api.sln` on the current working tree produces **5 compiler errors** plus several latent runtime/structural mismatches caused by the partial `Book -> Edition -> PlatformEditionMetadata` refactor:

### Issue 1: Duplicate Scratch `DbContext` & Broken Inline `Edition` Mapping
- **Symptom:**
  - An untracked scratch file at `src/PublishingTracker.Infrastructure/Data/PublishingTrackerDbContext.cs` defines a second `PublishingTrackerDbContext` (missing `ImportJobs`, `ICurrentUserService`, global filters, and `SaveChangesAsync` audit logic) **and** a duplicate `Edition` class in namespace `PublishingTracker.Infrastructure.Data`.
  - Meanwhile, the real `src/PublishingTracker.Infrastructure/Persistence/PublishingTrackerDbContext.cs` still has inline `modelBuilder.Entity<Edition>(...)` configuration referencing `e.Isbn` (wrong casing vs. `Edition.ISBN`) and properties removed from `Edition.cs` (`Title`, `Subtitle`, `Publisher`, `Language`, `EditionType`, `DeliveryMethod`, `VolumeNumber`, `Audience`, `LitChartId`).
  - At the same time, three clean `IEntityTypeConfiguration<T>` classes already exist under `src/PublishingTracker.Infrastructure/Data/Configurations/` (`BookConfiguration.cs`, `EditionConfiguration.cs`, `PlatformEditionMetadataConfiguration.cs`) matching `Book.cs`, `Edition.cs`, and `PlatformEditionMetadata.cs`.
- **Fix:**
  1. Delete the scratch `src/PublishingTracker.Infrastructure/Data/PublishingTrackerDbContext.cs`.
  2. Move `BookConfiguration.cs`, `EditionConfiguration.cs`, and `PlatformEditionMetadataConfiguration.cs` into `src/PublishingTracker.Infrastructure/Persistence/Configurations/` (updating their namespace to `PublishingTracker.Infrastructure.Persistence.Configurations`).
  3. Update `Persistence/PublishingTrackerDbContext.cs` to apply those configuration classes via `modelBuilder.ApplyConfiguration(...)` instead of the stale inline `Book` / `Edition` / `PlatformEditionMetadata` blocks.
  4. Expose `IQueryable<Edition> Editions { get; }` and `IQueryable<PlatformEditionMetadata> PlatformEditionMetadata { get; }` on `IApplicationDbContext` (`src/PublishingTracker.Application/Interfaces/IApplicationDbContext.cs`) and implement them explicitly in `Persistence/PublishingTrackerDbContext.cs`.

### Issue 2: Unfinished `Book.AddEdition` & `Edition.Update` (`Narrator` / `Duration`)
- **Symptom:** In `Book.cs` (`lines 90–95`), `Book.AddEdition(...)` accepts `narrator` and `duration` parameters, calls `edition.Update(isbn, publicationDate, basePrice, pageCount, dimensions, fileSize)`, and leaves an empty `if (narrator != null || duration != null)` comment block—silently dropping audiobook `Narrator` and `Duration`.
- **Fix:** Extend `Edition.Update(...)` to accept optional `string? narrator = null, TimeSpan? duration = null` and assign `Narrator = narrator; Duration = duration;`, and pass both from `Book.AddEdition(...)`.

### Issue 3: Broken Book Command/Query Handlers & Repository Includes
- **Current Build Errors:**
  - `UpdateBookCommandHandler.cs(34)`: `CS1503` calling `book.Update(...)` with old 7-argument signature `(Title, Author, ISBN, PublicationDate, BasePrice, Genre, Description)`.
  - `GetBookByIDQueryHandler.cs(41–43)`: `CS1061` reading `book.ISBN`, `book.PublicationDate`, and `book.BasePrice` directly from `Book`.
- **Latent Repository Issue:** `BookRepository.GetByIdAsync(int id, CancellationToken)` only does `.Include(b => b.User)` without loading `b.Editions` or `b.Sales`, so `GetBookByIDQueryHandler` would compute `0` sales and `null` edition fields even after fixing compilation.
- **Fix:**
  1. Update `BookRepository.GetByIdAsync(int id, CancellationToken)` to include `.Include(b => b.Editions).ThenInclude(e => e.PlatformMetadata)` and `.Include(b => b.Sales)`.
  2. Update `GetBookByIDQueryHandler.cs` to project `ISBN`, `PublicationDate`, and `BasePrice` from `book.Editions.OrderBy(e => e.Id).FirstOrDefault()` (consistent with `BookRepository.GetAllAsync`), preserving `IntegrationTests.UpdateBook_SavesAllOptionalFields`.
  3. Update `UpdateBookCommandHandler.cs`:
     - Replace `book.VerifyOwnership(userId.Value)` (which throws an unhandled exception) or return `Result<int>.Failure("Access denied.")` when `book.UserId != userId.Value` so `BookEndpoints.cs` maps it cleanly to `403 Forbidden`.
     - Call `book.Update(request.Title, request.Author, book.SeriesName, book.SeriesNumber, request.Genre, request.Description, book.CoverImageUrl)`.
     - If `request.ISBN != null || request.PublicationDate != null || request.BasePrice != null`, update the primary `Edition` (`book.Editions.OrderBy(e => e.Id).FirstOrDefault()`) or call `book.AddEdition(EditionFormat.Paperback, request.ISBN, request.PublicationDate, request.BasePrice)` if no edition exists yet.

### Issue 4: `ImportCsvCommandHandler.cs` Empty Author Invariant Violation
- **Symptom:** `Book` constructor (`Book.cs:42–43`) now throws `ArgumentException("Author is required")` when `author` is blank. In `ImportCsvCommandHandler.cs(68)`, auto-creating a missing book during CSV import passes `string.Empty` for `author`, which will fail `ImportIntegrationTests`.
- **Fix:** Pass `"Unknown Author"` (or a parsed author column if present) when creating a book on the fly in `ImportCsvCommandHandler.cs`.

### Issue 5: Untracked Duplicate DTO & Scratch Config Cleanup
- **Symptom:**
  - `src/PublishingTracker.Application/DTOs/CurrencyTotalDto.cs` duplicates `Common/Dtos/CurrencyTotalDto.cs` under namespace `PublishingTracker.Api.Models.Dtos` (zero references found across `.cs` files).
  - `Publishing-Tracker/apps/api/appsettings.json` is an untracked copy outside `src/PublishingTracker.Api/`.
- **Fix:** Delete the unused duplicate `Application/DTOs/CurrencyTotalDto.cs` and remove the stray root `apps/api/appsettings.json` (keeping all API configuration under `src/PublishingTracker.Api/`).

---

## 3. Workstream 1 — Upgrade to `.NET 10 LTS` (`net10.0`)

`.NET 9` reached end-of-support on November 10, 2026; `.NET 10.0.404` SDK is already installed locally and used by `ai-code-review-bot`.

1. **`Publishing-Tracker/apps/api/Directory.Build.props`:**
   ```xml
   <Project>
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
   </Project>
   ```
2. **`Publishing-Tracker/apps/api/Directory.Packages.props`:**
   - Upgrade ASP.NET Core / EF Core / Npgsql packages from `8.0.x` / `9.0.x` (`Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.*`, `Microsoft.Extensions.Caching.Memory`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`) to verified `.NET 10`-compatible stable versions during `dotnet restore`.
   - Remove `Microsoft.EntityFrameworkCore.SqlServer` once PostgreSQL standardization is applied.
3. **`.github/workflows/api.yml`:**
   - Change `dotnet-version: "9.0.x"` to `"10.0.x"`.
   - Check `.github/workflows/monorepo.yml` for any `dotnet-version` references and update similarly if present.

---

## 4. Workstream 2 — PostgreSQL Standardization (Local Docker, Tests, Migrations, & CI)

Currently, `Program.cs` uses `UseNpgsql` in Azure (external Neon) and `UseSqlServer` locally, `TestWebAppFactory.cs` hardcodes SQL Server LocalDB (`Server=(localdb)\\mssqllocaldb`), and `PlatformEditionMetadataConfiguration` uses PostgreSQL-specific `.HasColumnType("jsonb")`.

### 4.1 Project & `Program.cs` Changes
1. Remove `Microsoft.EntityFrameworkCore.SqlServer` package references from `PublishingTracker.Api.csproj` and `PublishingTracker.Infrastructure.csproj`; add `Npgsql.EntityFrameworkCore.PostgreSQL` to `PublishingTracker.Infrastructure.csproj`.
2. In `PublishingTracker.Api/Program.cs`:
   - For all non-`Testing` environments, configure `UseNpgsql(...)` using `NeonConnection` (Azure/prod) or `DefaultConnection` (local PostgreSQL).
   - Keep `EnableRetryOnFailure()` via `npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()`.
   - Remember PostgreSQL `timestamp with time zone` rules: ensure persisted `DateTime` values (`PublicationDate`, `SaleDate`, `PublishedDate`) are normalized to `DateTimeKind.Utc` on write/import or configured explicitly so `Npgsql` never rejects `DateTimeKind.Unspecified` in tests (`new DateTime(2024, 1, 1)` in `IntegrationTests.cs`!).
     > **Important Detail:** `IntegrationTests.cs` lines 106 and 135 construct `new System.DateTime(2023, 1, 1)` (`DateTimeKind.Unspecified`). Under `Npgsql` 6+, writing `DateTimeKind.Unspecified` to a `timestamptz` column throws `ArgumentException` unless either:
     > - Dates are converted via `DateTime.SpecifyKind(d, DateTimeKind.Utc)` / `.ToUniversalTime()` in `Edition`/`Sale` or DTO/EF value converters, **or**
     > - Columns that represent pure calendar dates use `date` / `timestamp without time zone`.
     > Adding a clean UTC normalization helper/converter in Phase 0 prevents subtle `Npgsql` runtime test failures!

### 4.2 EF Core Migrations Reset for PostgreSQL
- Existing migrations in `src/PublishingTracker.Infrastructure/Migrations/` contain SQL Server column types (`nvarchar(max)`, `datetime2`, `int IDENTITY(1,1)`).
- Replace them with a clean PostgreSQL initial migration (`dotnet ef migrations add InitialPostgres`) generated against `Npgsql`, verified to include:
  - `Users`, `Books`, `Editions`, `PlatformEditionMetadata` (`MetadataJson` as `jsonb`), `Platforms`, `Sales`, `ImportJobs`.

### 4.3 Local Docker, `TestWebAppFactory.cs`, and CI Service Container
1. **`docker-compose.yml` (local dev/test DB):** Run `pgvector/pgvector:pg17` (which serves standard PostgreSQL now and is ready for `pgvector` in Phase 4 without switching images later).
2. **`TestWebAppFactory.cs`:**
   - Replace `UseSqlServer("Server=(localdb)\\mssqllocaldb...")` with `UseNpgsql(...)` reading `Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")` with a sensible local Docker fallback (`Host=localhost;Port=5432;Database=publishingtracker_tests;Username=postgres;Password=postgres`).
   - Run `dbContext.Database.EnsureDeleted(); dbContext.Database.Migrate();` (or `EnsureCreated()`) cleanly once per test run.
3. **`.github/workflows/api.yml`:**
   - Add a `services.postgres` container using image `pgvector/pgvector:pg17` on port `5432` and pass `TEST_POSTGRES_CONNECTION` to `dotnet test`.

---

## 5. Workstream 3 — Authentication & Tenant Isolation Hygiene

### 5.1 `TokenService.cs` (`iat` + 60-Minute UTC Expiry)
Currently, `TokenService.cs` issues 7-day local-time JWTs (`DateTime.Now.AddDays(7)`) without an `iat` claim, causing `RoleValidationMiddleware.cs` (`line 28`) to skip role-revocation checks (`iatClaim != null`).

Update `TokenService.CreateToken(User user)`:
```csharp
var now = DateTimeOffset.UtcNow;
var expiryMinutes = _configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 60;

var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
    new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
    new(JwtRegisteredClaimNames.Email, user.Email),
    new(JwtRegisteredClaimNames.GivenName, user.FirstName),
    new(ClaimTypes.Surname, user.LastName),
    new(ClaimTypes.Role, user.Role)
};

// ...
var token = new JwtSecurityToken(
    issuer: _configuration["Jwt:Issuer"],
    audience: _configuration["Jwt:Audience"],
    claims: claims,
    notBefore: now.UtcDateTime,
    expires: now.UtcDateTime.AddMinutes(expiryMinutes),
    signingCredentials: creds
);
```
Add `"Jwt": { "ExpiryMinutes": 60 }` to `appsettings.json` and `appsettings.Testing.json`. Add a unit test verifying `iat` is present as an integer epoch claim and `exp` is `~60` minutes from `iat`.

### 5.2 Explicit Owner Checks & Defense-in-Depth Query Filters
1. In `Persistence/PublishingTrackerDbContext.cs`, add defense-in-depth global query filters on `Edition` (`e.Book.UserId == _currentUserService.UserId`) and `PlatformEditionMetadata` (`pm.Edition.Book.UserId == _currentUserService.UserId`) alongside `Book`, `Sale`, and `ImportJob`.
2. **Mandatory Engineering Rule:** Never rely on global query filters as the primary authorization gate (since `_currentUserService.UserId == null` bypasses them for background workers). Every Application query and Phase 1 MCP handler must:
   - Assert `var userId = _currentUserService.UserId ?? throw ...` (or return Unauthorized `Result`), and
   - Include an explicit `.Where(b => b.UserId == userId)` predicate.

---

## 6. Workstream 4 — Locked Contracts, Completeness Spec, & Seed Data

### 6.1 `FormatCompletenessRules` Specification (To Implement in `PublishingTracker.Application`)
Codify the locked rules from [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md) as pure domain/application logic so unit tests and Phase 1 SQL/LINQ projections use one source of truth:

- **Helper:** `Missing(string? s) => string.IsNullOrWhiteSpace(s)`
- **Book-level missing fields (`MissingBookFields`):**
  - `"Description"` if `Missing(book.Description)`
  - `"Genre"` if `Missing(book.Genre)`
  - `"CoverImageUrl"` if `Missing(book.CoverImageUrl)`
  - `"Editions"` if `book.Editions.Count == 0`
  - `"SeriesNumber"` if `!Missing(book.SeriesName)` and `(book.SeriesNumber is null or <= 0)`
- **Edition-level missing fields (`MissingEditionFields`):**
  - **All formats:**
    - `"PublicationDate"` if `edition.PublicationDate is null`
    - `"BasePrice"` if `edition.BasePrice is null` (`>= 0m` is valid; `0.00m` is allowed)
  - **Print (`Paperback`, `Hardcover`, `LargeText`):**
    - `"ISBN"` if `Missing(edition.ISBN)`
    - `"PageCount"` if `edition.PageCount is null or <= 0`
    - `"Dimensions"` if `Missing(edition.Dimensions)`
  - **Digital (`Ebook`):**
    - `"ISBN_Or_PlatformProductId"` if `Missing(edition.ISBN)` and `!edition.PlatformMetadata.Any(pm => !Missing(pm.PlatformProductId))`
    - `"FileSize"` if `edition.FileSize is null or <= 0`
  - **Audio (`Audiobook`):**
    - `"ISBN_Or_PlatformProductId"` if `Missing(edition.ISBN)` and `!edition.PlatformMetadata.Any(pm => !Missing(pm.PlatformProductId))`
    - `"Narrator"` if `Missing(edition.Narrator)`
    - `"Duration"` if `edition.Duration is null or <= TimeSpan.Zero`

### 6.2 Phase 1 Endpoint & MCP Contract Freeze

| Endpoint / Tool | Request | Response Shape | Error Semantics |
|---|---|---|---|
| `GET /api/books/missing-metadata` / `find_titles_missing_metadata` | `format?` (`EditionFormat`), `missingField?` (`string`), `page` (`>= 1`, default `1`), `pageSize` (`1..50`, default `20`) | `{ page, pageSize, totalCount, hasMore, items: [{ bookId, title, missingBookFields[], editions: [{ editionId, format, missingFields[] }] }] }` | `400 INVALID_ARGUMENT` for bad enum/page bounds; `401` on missing/invalid JWT |
| `GET /api/books/{bookId}/details` / `get_title_details` | `bookId` (`> 0`) | `{ bookId, title, author, seriesName, seriesNumber, genre, description, coverImageUrl, editions: [{ editionId, format, isbn, publicationDate, basePrice, pageCount, dimensions, fileSize, narrator, duration, platformSummaries: [{ platformId, platformName, isPublished, publishedDate, platformProductId }] }] }` | `404 NOT_FOUND_OR_UNAUTHORIZED` whether book is absent or owned by another user (never reveals cross-user IDs) |

### 6.3 External LLM Data-Handling Policy (Documented for Phase 2+)
- **Permitted outbound payload fields:** `Book` catalogue metadata (`Title`, `Author`, `SeriesName`, `SeriesNumber`, `Genre`, `Description`), `Edition` specifications, `PlatformEditionMetadata.IsPublished`/`PlatformProductId`, and non-sensitive markdown guideline chunks.
- **Prohibited outbound payload fields:** `User.Email`, password hashes, `TokenService` JWTs, internal connection strings, and raw `HttpContext` headers.
- **OpenRouter request settings:** Enforce `provider.data_collection = "deny"` and pin a tested Claude model/provider configuration; set `EnableSensitiveData = false` on OpenTelemetry spans.

### 6.4 Two-User Non-Sensitive Seed Catalogue (For Phase 0–5 Testing)
Create a deterministic test/dev seed helper (`TwoUserSeedCatalog`) with **synthetic, non-PII data**:
- **User A (`author-a@example.test`, role `User`):**
  - *Complete Book (`Paperback` + `Ebook` with `PlatformProductId`)* — ensures complete titles do **not** appear in `find_titles_missing_metadata`.
  - *Book missing `Description`, `CoverImageUrl`, and `SeriesNumber` (has `SeriesName = "Clockwork Isles"`).*
  - *Book with an `Audiobook` edition missing `Narrator` and `Duration` (has `PlatformProductId`, no `ISBN` — verifies `Ebook`/`Audiobook` identifier rule).*
  - *Book with `BasePrice = 0.00m` `Ebook` (valid free promo — must NOT report `BasePrice` missing).*
  - *Book with `0` editions (`missingBookFields: ["Editions"]`).*
- **User B (`author-b@example.test`, role `User`):**
  - *Book with incomplete `Hardcover` (`ISBN`, `PageCount`, `Dimensions` missing)* — proves User A cannot discover or fetch User B's books via API, `/mcp`, or Agent.

---

## 7. Execution Sequence & Verification Checklist

Execute Phase 0 in this order:
1. **Step 0.1 — Clean scratch files & upgrade TFM:** Delete duplicate `Infrastructure/Data/PublishingTrackerDbContext.cs`, `Application/DTOs/CurrencyTotalDto.cs`, and root `apps/api/appsettings.json`; move EF configurations to `Persistence/Configurations/`; set `net10.0` in `Directory.Build.props` and `.github/workflows/api.yml`; update `Directory.Packages.props`.
2. **Step 0.2 — Fix domain, handlers, and repository:** Fix `Book.AddEdition` / `Edition.Update`, `Persistence/PublishingTrackerDbContext.cs` (`ApplyConfiguration` + `Editions`/`PlatformEditionMetadata` query filters), `IApplicationDbContext`, `BookRepository`, `GetBookByIDQueryHandler`, `UpdateBookCommandHandler`, and `ImportCsvCommandHandler`.
3. **Step 0.3 — Standardize on PostgreSQL:** Remove `SqlServer` packages, update `Program.cs` and `TestWebAppFactory.cs` to `UseNpgsql`, add UTC `DateTime` normalization, regenerate clean PostgreSQL migrations, and configure local Docker (`pgvector/pgvector:pg17`) + CI `postgres` service.
4. **Step 0.4 — Auth hygiene & completeness rules:** Update `TokenService.cs` (`iat` + `Jwt:ExpiryMinutes = 60` UTC), implement `FormatCompletenessRules` + unit tests + `TwoUserSeedCatalog`, and freeze contract docs.

### Verification Commands (Run Before Closing Phase 0)
```powershell
# 1. Start local PostgreSQL container (if not already running)
docker compose up -d postgres

# 2. Restore, build, and run full test suite on .NET 10 against PostgreSQL
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
dotnet restore Publishing-Tracker/apps/api/PublishingTracker.Api.sln
dotnet build Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-restore --configuration Release
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln --no-build --configuration Release --verbosity normal
```

### Phase 0 Acceptance Checklist
- [x] `dotnet build` succeeds with `0` errors on `net10.0` (including `20261117201611_InitialPostgres` PostgreSQL migration)
- [x] All `41/41` tests in `PublishingTracker.Api.Tests` (`38` existing + `3` new `Phase0BaselineTests`) pass locally (using `TestWebAppFactory` EF `InMemory` fallback when `TEST_POSTGRES_CONNECTION` is unset; configured for `pgvector/pgvector:pg17` via `TEST_POSTGRES_CONNECTION` in Docker/CI)
- [x] No SQL Server dependencies remain in `PublishingTracker.Api`, `PublishingTracker.Infrastructure`, or `TestWebAppFactory`
- [x] `.github/workflows/api.yml` and `.github/workflows/monorepo.yml` target `.NET 10.0.x` with a `pgvector/pgvector:pg17` service container
- [x] `TwoUserSeedCatalog` and locked Phase 1 API/MCP contracts are reviewed and ready for Phase 1

*(Note: `dotnet ef` `9.0.8` generated `20261117201611_InitialPostgres` cleanly; optionally run `dotnet tool update --global dotnet-ef --version 10.*` to match the EF Core `10.0.9` runtime for future migrations.)*
