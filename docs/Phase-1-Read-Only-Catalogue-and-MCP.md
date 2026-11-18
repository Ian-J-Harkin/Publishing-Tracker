# Phase 1 — Expanded Design: Read-Only Catalogue Capabilities & Hosted `/mcp` Endpoint

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), and [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md).

---

## 1. Purpose, Scope, and Boundaries

### Goal
Expose two owner-scoped, read-only catalogue tools (`find_titles_missing_metadata` and `get_title_details`) both over authenticated REST routes on `PublishingTracker.Api` and via a standards-compliant Streamable HTTP MCP endpoint hosted at `/mcp` inside `PublishingTracker.Api` (`ADR-001`, `ADR-005`). Prove discovery, structured results, bounded pagination, input validation, and **two-user isolation** using a real `McpClient` (`ModelContextProtocol` `1.3.0`).

### In Scope
1. Two new MediatR CQRS read queries (`FindTitlesMissingMetadataQuery` and `GetTitleDetailsQuery`) and DTOs in `PublishingTracker.Application` backed by Phase 0's [FormatCompletenessRules.cs](../Publishing-Tracker/apps/api/src/PublishingTracker.Application/Books/Services/FormatCompletenessRules.cs) and `IApplicationDbContext`.
2. Two new read-only REST endpoints in `PublishingTracker.Api/Endpoints/BookEndpoints.cs` (`GET /api/books/missing-metadata` and `GET /api/books/{id:int}/details`).
3. A new in-repo MCP library project `src/PublishingTracker.Mcp/PublishingTracker.Mcp.csproj` containing `CatalogueMcpTools` (`[McpServerToolType]`), referenced by `PublishingTracker.Api`.
4. Hosting the MCP server at `/mcp` in `PublishingTracker.Api/Program.cs` using `ModelContextProtocol.AspNetCore` (`1.3.0`) with `app.MapMcp("/mcp").RequireAuthorization()`.
5. Unit tests and `TestWebAppFactory` + `McpClient` (`HttpClientTransport`) integration tests in `PublishingTracker.Api.Tests` covering discovery, tool calls, filters, pagination, error mapping, and two-user isolation.

### Out of Scope for Phase 1
- `PublishingTracker.Agent` host, `IChatClient` loop, OpenRouter integration (Phase 2).
- `EvidenceLedger` and Stage 2 structured-output verification (Phase 3).
- `pgvector` RAG ingestion and `RagSearchTool` (Phase 4).
- Historical pricing (`get_price_on_date`), edition revision logs (`get_edition_history`), or title lifecycle status (`list_titles_by_status`) (Deferred milestone).

---

## 2. Verified MCP SDK Packages & APIs (`.NET 10`)

Checked against the locally cached `ModelContextProtocol` `1.3.0` and `ModelContextProtocol.Core` `1.3.0` (`net10.0` target group):

| Package | Version | Project | Purpose |
|---|---|---|---|
| `ModelContextProtocol` | `1.3.0` | `PublishingTracker.Mcp` & `PublishingTracker.Api.Tests` | `[McpServerToolType]`, `[McpServerTool]`, `McpException`, and `McpClient` + `HttpClientTransport` for integration tests |
| `ModelContextProtocol.AspNetCore` | `1.3.0` | `PublishingTracker.Api` | `WithHttpTransport(...)` and `app.MapMcp("/mcp")` Streamable HTTP hosting |

*(Note: `ModelContextProtocol` `1.3.0` and `ModelContextProtocol.Core` `1.3.0` are already cached locally; `ModelContextProtocol.AspNetCore` `1.3.0` will be restored during Phase 1 setup. Centralize both versions in `Directory.Packages.props`.)*

### Verified SDK Primitives (`1.3.0`)
- **Server registration (`Program.cs`):**
  ```csharp
  builder.Services
      .AddMcpServer()
      .WithHttpTransport(options =>
      {
          // Stateless HTTP turns: each POST /mcp executes inside its own authenticated ASP.NET Core HttpContext
          options.Stateless = true;
      })
      .WithTools<CatalogueMcpTools>();
  ```
- **Endpoint mapping (`Program.cs`, after `UseAuthentication()`, `UseAuthorization()`, `UseRoleValidation()`):**
  ```csharp
  app.MapMcp("/mcp").RequireAuthorization();
  ```
- **Tool metadata (`CatalogueMcpTools`):**
  - `[McpServerToolType]`
  - `[McpServerTool(Name = "find_titles_missing_metadata", ReadOnly = true, Idempotent = true, Destructive = false)]`
  - `[McpServerTool(Name = "get_title_details", ReadOnly = true, Idempotent = true, Destructive = false)]`
- **Standalone client in tests (`McpClient`):**
  ```csharp
  var transport = new HttpClientTransport(
      new HttpClientTransportOptions
      {
          Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
          TransportMode = HttpTransportMode.StreamableHttp
      },
      httpClient);

  await using var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: ct);
  var tools = await mcpClient.ListToolsAsync(cancellationToken: ct);
  ```

---

## 3. Workstream 1 — Application Layer: Read-Only CQRS Queries & DTOs

### 3.1 `FindTitlesMissingMetadataQuery`
Location: `src/PublishingTracker.Application/Books/Queries/FindTitlesMissingMetadataQuery.cs`

#### Request & Response DTOs
```csharp
public record FindTitlesMissingMetadataQuery(
    string? Format = null,
    string? MissingField = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<MissingMetadataPageDto>>;

public sealed record MissingEditionFieldsDto(
    int EditionId,
    string Format,
    IReadOnlyList<string> MissingFields);

public sealed record MissingTitleItemDto(
    int BookId,
    string Title,
    IReadOnlyList<string> MissingBookFields,
    IReadOnlyList<MissingEditionFieldsDto> Editions);

public sealed record MissingMetadataPageDto(
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore,
    IReadOnlyList<MissingTitleItemDto> Items);
```

#### Validation & Explicit Owner Scoping Rules
1. **Explicit caller identity check:**
   ```csharp
   var userId = _currentUserService.UserId;
   if (userId is null)
       return Result<MissingMetadataPageDto>.Failure("UNAUTHORIZED: Authenticated user required.");
   ```
   Never rely solely on EF global query filters (`ADR-004`/`ADR-009`). Always pair with `.Where(b => b.UserId == userId.Value)`.
2. **Input validation (actionable messages):**
   - `Page < 1` -> `"INVALID_ARGUMENT: Page must be greater than or equal to 1."`
   - `PageSize < 1 || PageSize > 50` -> `"INVALID_ARGUMENT: PageSize must be between 1 and 50."`
   - Non-empty `Format`: parse case-insensitively against `Enum.TryParse<EditionFormat>(Format, ignoreCase: true, out var parsedFormat)`. On failure -> `"INVALID_ARGUMENT: Unsupported format '{Format}'. Allowed values: Ebook, Paperback, Hardcover, Audiobook, LargeText."`
   - Non-empty `MissingField`: validate against known completeness field names (`Description`, `Genre`, `CoverImageUrl`, `Editions`, `SeriesNumber`, `PublicationDate`, `BasePrice`, `ISBN`, `PageCount`, `Dimensions`, `ISBN_Or_PlatformProductId`, `FileSize`, `Narrator`, `Duration`). On unknown field -> `"INVALID_ARGUMENT: Unsupported missingField '{MissingField}'."`
3. **Server-side evaluation & filtering (`AsNoTracking`):**
   - Query `_db.Books.AsNoTracking().Where(b => b.UserId == userId.Value).Include(b => b.Editions).ThenInclude(e => e.PlatformMetadata).OrderBy(b => b.Id)`.
   - Evaluate each book with `FormatCompletenessRules.EvaluateBook(book)`.
   - **When `Format` filter is set:** project `Editions` to only those matching `parsedFormat` whose `MissingFields.Count > 0` (excluding `MissingBookFields` when searching specifically for incomplete editions of a format, or wait—define exact deterministic rule below):
     - *Exact rule:* Keep editions where `e.Format == parsedFormat && e.MissingFields.Count > 0`. Include the book if `matchingEditions.Count > 0` (or if `MissingField` matches a book-level field when `Format` is omitted). When `Format` is `null`, keep all editions of the book with `MissingFields.Count > 0` and keep the book whenever `eval.IsIncomplete` is `true`.
   - **When `MissingField` filter is set:** keep only books where `MissingBookFields` contains `MissingField` (when `Format` is `null`) OR at least one matching edition's `MissingFields` contains `MissingField` (case-insensitive). Trim each edition list to editions containing `MissingField`.
   - **Pagination:** Compute `totalCount` after server-side filtering, slice `.Skip((Page - 1) * PageSize).Take(PageSize)`, and set `HasMore = (Page * PageSize) < totalCount`.

### 3.2 `GetTitleDetailsQuery`
Location: `src/PublishingTracker.Application/Books/Queries/GetTitleDetailsQuery.cs`

#### Request & Response DTOs
```csharp
public record GetTitleDetailsQuery(int BookId) : IRequest<Result<TitleDetailsDto>>;

public sealed record PlatformSummaryDto(
    int PlatformId,
    string PlatformName,
    bool IsPublished,
    DateTime? PublishedDate,
    string? PlatformProductId);

public sealed record EditionDetailsDto(
    int EditionId,
    string Format,
    string? ISBN,
    DateTime? PublicationDate,
    decimal? BasePrice,
    int? PageCount,
    string? Dimensions,
    decimal? FileSize,
    string? Narrator,
    TimeSpan? Duration,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<PlatformSummaryDto> PlatformSummaries);

public sealed record TitleDetailsDto(
    int BookId,
    string Title,
    string Author,
    string? SeriesName,
    int? SeriesNumber,
    string? Genre,
    string? Description,
    string? CoverImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string> MissingBookFields,
    IReadOnlyList<EditionDetailsDto> Editions);
```

#### Validation & Cross-User Isolation
1. If `_currentUserService.UserId is not int userId` -> `"UNAUTHORIZED: Authenticated user required."`
2. If `request.BookId <= 0` -> `"INVALID_ARGUMENT: bookId must be a positive integer."`
3. Query:
   ```csharp
   var book = await _db.Books
       .AsNoTracking()
       .Where(b => b.Id == request.BookId && b.UserId == userId)
       .Include(b => b.Editions)
           .ThenInclude(e => e.PlatformMetadata)
               .ThenInclude(pm => pm.Platform)
       .FirstOrDefaultAsync(cancellationToken);
   ```
4. If `book is null` (whether absent OR owned by another user), return:
   `Result<TitleDetailsDto>.Failure("NOT_FOUND_OR_UNAUTHORIZED: Title was not found or is not accessible.")`
   This guarantees identical error responses for missing vs. non-owned IDs (`ADR-009`).

---

## 4. Workstream 2 — REST Endpoints, `PublishingTracker.Mcp`, & `/mcp` Hosting

### 4.1 New Project: `src/PublishingTracker.Mcp/PublishingTracker.Mcp.csproj`
- References: `PublishingTracker.Application` + `ModelContextProtocol` (`1.3.0`).
- Added to `PublishingTracker.Api.sln` and referenced by `PublishingTracker.Api` and `PublishingTracker.Api.Tests`.
- Contains `Tools/CatalogueMcpTools.cs`:

```csharp
using System.ComponentModel;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PublishingTracker.Application.Books.Queries;

namespace PublishingTracker.Mcp.Tools;

[McpServerToolType]
public sealed class CatalogueMcpTools(ISender mediator)
{
    [McpServerTool(
        Name = "find_titles_missing_metadata",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("Find books and editions owned by the authenticated caller that are missing required metadata fields. Returns stable bookId and editionId values, missing field names, and bounded pagination metadata.")]
    public async Task<MissingMetadataPageDto> FindTitlesMissingMetadataAsync(
        [Description("Optional edition format filter: Ebook, Paperback, Hardcover, Audiobook, or LargeText.")]
        string? format = null,
        [Description("Optional missing field name filter (e.g. Description, Genre, CoverImageUrl, Editions, SeriesNumber, ISBN, PageCount, Dimensions, ISBN_Or_PlatformProductId, FileSize, Narrator, Duration, PublicationDate, BasePrice).")]
        string? missingField = null,
        [Description("1-based page number (default 1).")]
        int page = 1,
        [Description("Items per page from 1 to 50 (default 20).")]
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new FindTitlesMissingMetadataQuery(format, missingField, page, pageSize),
            cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? result.Value
            : throw new McpException(result.ErrorMessage ?? "Failed to query missing metadata.");
    }

    [McpServerTool(
        Name = "get_title_details",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("Get full book metadata, all editions, format completeness flags, and platform publication summaries for a single bookId owned by the authenticated caller.")]
    public async Task<TitleDetailsDto> GetTitleDetailsAsync(
        [Description("Positive integer Book ID owned by the caller.")]
        int bookId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTitleDetailsQuery(bookId), cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? result.Value
            : throw new McpException(result.ErrorMessage ?? "Failed to load title details.");
    }
}
```

**Security invariants enforced on `CatalogueMcpTools`:**
- Zero `userId` or tenant parameters in either method signature (`ADR-009`).
- `Stateless = true` HTTP transport ensures each `/mcp` POST request runs inside ASP.NET Core's normal scoped DI container, so `ICurrentUserService` reads `HttpContext.User` from that request's validated Bearer JWT.
- Only read-only MediatR queries are injected; no `DbContext`, write repository, or delete/update commands exist in `PublishingTracker.Mcp`.

### 4.2 REST Endpoints in `BookEndpoints.cs`
Add two read-only routes on `booksGroup` (`/api/books`, already protected by `.RequireAuthorization()`):
- `GET /api/books/missing-metadata` (`[FromQuery] string? format, [FromQuery] string? missingField, [FromQuery] int page = 1, [FromQuery] int pageSize = 20`)
- `GET /api/books/{id:int}/details` (also consider adding `:int` constraint on existing `/{id}` routes if safe so `"missing-metadata"` never collides)

Map `Result.ErrorMessage` prefixes cleanly using `ApiResults`:
- Starts with `"UNAUTHORIZED:"` -> `ApiResults.Unauthorized(msg)` (`401`)
- Starts with `"INVALID_ARGUMENT:"` -> `ApiResults.BadRequest(msg)` (`400`)
- Starts with `"NOT_FOUND_OR_UNAUTHORIZED:"` -> `ApiResults.NotFound(msg)` (`404`)

### 4.3 `PublishingTracker.Api/Program.cs` Wiring
1. In service registration:
   ```csharp
   builder.Services
       .AddMcpServer()
       .WithHttpTransport(options => options.Stateless = true)
       .WithTools<CatalogueMcpTools>();
   ```
2. In middleware pipeline (after `app.UseAuthentication(); app.UseAuthorization(); app.UseRoleValidation();`):
   ```csharp
   app.MapMcp("/mcp").RequireAuthorization();
   ```
3. Verify CORS / Swagger / `UseRoleValidation` interaction:
   - `RoleValidationMiddleware` runs on `/mcp` requests too, enforcing `RoleChangedAt` checks whenever the JWT carries `iat` (added in Phase 0).
   - No `AllowAnonymous` is applied anywhere on `/mcp`.

---

## 5. Workstream 3 — Unit & Integration Test Plan (`PublishingTracker.Api.Tests`)

Add `Phase1McpAndCatalogueTests.cs` using `TestWebAppFactory` + `TwoUserSeedCatalog` + real `ModelContextProtocol.Client.McpClient` (`HttpClientTransport` pointing at `/mcp`):

### Required Test Cases
1. **`McpEndpoint_WithoutBearerToken_Returns401Unauthorized`**
   - Sends an unauthenticated `POST /mcp` request (with MCP initialize JSON-RPC payload) and asserts `HttpStatusCode.Unauthorized`.
2. **`McpClient_ListTools_DiscoversOnlyExpectedReadOnlyToolsWithoutTenantParams`**
   - Authenticates as User A (`author-a@example.test`), connects `McpClient` over `HttpClientTransport(HttpTransportMode.StreamableHttp)` to `/mcp`, calls `ListToolsAsync()`.
   - Asserts exactly 2 tools (`find_titles_missing_metadata`, `get_title_details`), both marked read-only (`ReadOnlyHint == true` / `DestructiveHint == false`), and asserts neither tool's JSON input schema contains `userId`, `ownerId`, or `callerUserId`.
3. **`McpClient_FindTitlesMissingMetadata_ReturnsIncompleteUserABooksAndRespectsFilters`**
   - Seeds `TwoUserSeedCatalog` (User A: 5 books—2 complete including free promo `BasePrice = 0m`, 3 incomplete; User B: 1 incomplete book).
   - Calls `find_titles_missing_metadata` as User A via `McpClient.CallToolAsync`:
     - Unfiltered returns `totalCount == 3`, excludes `"Complete Catalogue Title"`, `"Free Promo Ebook"`, and User B's `"Beta Private Incomplete Hardcover"`.
     - `format: "Audiobook"` returns only `"Audio Missing Narrator"` with `missingFields: ["Narrator", "Duration"]`.
     - `missingField: "SeriesNumber"` returns only `"Clockwork Missing Book Metadata"`.
4. **`McpClient_FindTitlesMissingMetadata_PaginatesCorrectlyWithHasMore`**
   - Calls `find_titles_missing_metadata` with `page: 1, pageSize: 2` (`totalCount: 3`, `hasMore: true`, `items.Count == 2`), then `page: 2, pageSize: 2` (`hasMore: false`, `items.Count == 1`).
5. **`McpClient_GetTitleDetails_ReturnsFullBookEditionsAndPlatformSummary`**
   - Calls `get_title_details` as User A on User A's `"Audio Missing Narrator"` bookId and verifies book metadata, edition details, `MissingFields`, and `PlatformSummaries` (`Kindle Direct`, `B0AUDIO0001`).
6. **`McpClient_GetTitleDetails_TwoUserIsolation_MasksOtherUsersBook`**
   - Calls `get_title_details` as User A using **User B's `bookId`**, and compares with a non-existent `bookId` (e.g., `999999`).
   - Asserts both return an MCP error (`IsError == true`) containing `"NOT_FOUND_OR_UNAUTHORIZED"`, proving User A cannot read or enumerate User B's books.
7. **`McpClient_InvalidArguments_ReturnsActionableErrors`**
   - Tests `page: 0`, `pageSize: 51`, `format: "Laserdisc"`, `missingField: "Bogus"`, and `bookId: 0` via `McpClient.CallToolAsync` and asserts `IsError == true` with actionable validation messages.
8. **`RestEndpoints_MissingMetadataAndDetails_MatchMcpContractsAndIsolation`**
   - Exercises `GET /api/books/missing-metadata` and `GET /api/books/{id}/details` directly for HTTP status codes (`200`, `400`, `401`, `404` on User B's book).

---

## 6. Execution Sequence & Verification Checklist

Execute Phase 1 in this order:
1. **Step 1.1 — Add MCP SDK packages:** Pin `ModelContextProtocol` (`1.3.0`) and `ModelContextProtocol.AspNetCore` (`1.3.0`) in `Directory.Packages.props`; create `src/PublishingTracker.Mcp/PublishingTracker.Mcp.csproj`, add it to `PublishingTracker.Api.sln`, and reference it from `PublishingTracker.Api` (plus add `ModelContextProtocol` to `PublishingTracker.Api.Tests`). Restore packages cleanly.
2. **Step 1.2 — Implement Application queries:** Add `FindTitlesMissingMetadataQuery.cs` and `GetTitleDetailsQuery.cs` (with handlers and DTOs) in `PublishingTracker.Application/Books/Queries/`.
3. **Step 1.3 — Implement `CatalogueMcpTools` & API hosting:** Add `CatalogueMcpTools.cs` in `PublishingTracker.Mcp/Tools/`, map `GET /api/books/missing-metadata` and `GET /api/books/{id:int}/details` in `BookEndpoints.cs`, and register `AddMcpServer().WithHttpTransport(...).WithTools<CatalogueMcpTools>()` + `app.MapMcp("/mcp").RequireAuthorization()` in `Program.cs`.
4. **Step 1.4 — Implement Phase 1 tests:** Add `Phase1McpAndCatalogueTests.cs` in `PublishingTracker.Api.Tests` covering all 8 scenarios above (including standalone `McpClient` and two-user isolation).
5. **Step 1.5 — Build & Verify:** Run `dotnet build` and `dotnet test` across the solution (`41` Phase 0 tests + Phase 1 tests passing with `0` failures).

### Phase 1 Acceptance Checklist
- [x] `PublishingTracker.Mcp` compiles cleanly on `net10.0` with `ModelContextProtocol` `1.3.0`
- [x] `/mcp` endpoint requires JWT auth (`401` when unauthenticated) and serves Streamable HTTP
- [x] Standalone `McpClient` discovers and invokes both `find_titles_missing_metadata` and `get_title_details`
- [x] Neither MCP tool schema exposes `userId` or any tenant parameter
- [x] `find_titles_missing_metadata` applies completeness rules server-side and returns bounded pagination (`hasMore`)
- [x] Two-user isolation tests prove User A cannot enumerate or inspect User B's titles via `/mcp` or REST
- [x] All tools are strictly read-only (`ReadOnly = true`, `Destructive = false`) and all tests pass (54/54 tests passing)

### Implementation Notes & Verified Adjustments
1. **SDK Roll-Forward (`global.json`):** Pinned SDK updated from `9.0.100` to `10.0.100` with `latestFeature` roll-forward to align with solution target `net10.0`.
2. **Package Version Alignments:**
   - `System.IdentityModel.Tokens.Jwt` pinned to `8.16.0` in `Directory.Packages.props` for `TokenService`.
   - `Swashbuckle.AspNetCore` upgraded to `10.2.3` to avoid runtime `TypeLoadException` against `Microsoft.OpenApi` 2.x in ASP.NET 10.
3. **Structured Content on MCP Tools:** Set `UseStructuredContent = true` on `[McpServerTool]` for both `find_titles_missing_metadata` and `get_title_details`, ensuring typed JSON objects are returned rather than serialized string blocks.
4. **Test Suite Execution:** All 54 tests pass cleanly (41 baseline tests + 13 new Phase 1 test cases across the 8 required scenarios). Server-side evaluation using `FormatCompletenessRules` guarantees format and missingField filters respect multi-edition data boundaries.
