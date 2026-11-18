# PublishingTracker MCP Server — Tester Guide

This guide details how to verify, test, and troubleshoot the **PublishingTracker Model Context Protocol (MCP)** server hosted at `/mcp`. It covers both automated test suites and hands-on manual verification using `curl`, PowerShell, and standalone clients.

---

## 1. Test Architecture & Coverage

The MCP implementation is covered by automated integration tests in `PublishingTracker.Api.Tests/Phase1McpAndCatalogueTests.cs` executed against `TestWebAppFactory` with a real `ModelContextProtocol.Client.McpClient` over Streamable HTTP:

```
┌────────────────────────────────────────────────────────┐
│ Phase1McpAndCatalogueTests                             │
│   ├── Real McpClient (HttpClientTransport)             │
│   └── Seed: TwoUserSeedCatalog (User A vs User B)      │
└──────────────────────────┬─────────────────────────────┘
                           │ Streamable HTTP (POST /mcp)
                           ▼
┌────────────────────────────────────────────────────────┐
│ TestWebAppFactory (In-Memory PostgreSQL Test Host)     │
│   ├── /mcp endpoint                                    │
│   └── Two-User Isolated Database Catalog               │
└────────────────────────────────────────────────────────┘
```

### The Two-User Test Matrix (`TwoUserSeedCatalog.cs`)
To prove non-model-controlled tenant isolation, the test fixture seeds a deterministic multi-user catalog:

| User | Book Title | Formats | State |
|---|---|---|---|
| **User A** (`author-a@example.test`) | *Complete Catalogue Title* | Paperback, Ebook | Complete (all fields present) |
| **User A** | *Free Promo Ebook* | Ebook | Complete (Price = $0.00 is valid) |
| **User A** | *Audio Missing Narrator* | Audiobook | **Incomplete:** Missing `Narrator`, `Duration` |
| **User A** | *Clockwork Missing Book Metadata* | Hardcover | **Incomplete:** Has `SeriesName` but missing `SeriesNumber` |
| **User A** | *Draft With No Editions* | None | **Incomplete:** Zero editions attached |
| **User B** (`beta-author@example.test`) | *Beta Private Incomplete Hardcover* | Hardcover | **Incomplete:** Owned exclusively by User B |

---

## 2. Automated Test Execution

### Running All Phase 1 Tests via CLI
Run the tests using standard `dotnet test` with telemetry and node-reuse flags to ensure clean terminal execution:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"; $env:DOTNET_NOLOGO="1"
dotnet test Publishing-Tracker/apps/api/PublishingTracker.Api.sln `
  --filter "FullyQualifiedName~Phase1" `
  -m:1 /nodeReuse:false /p:UseSharedCompilation=false
```

### Expected Output
```text
Test run for ...\PublishingTracker.Api.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13
```

*(Note: The test class contains 8 test methods, with `McpClient_InvalidArguments_ReturnsActionableErrors` running as an xUnit theory with 6 test cases, totaling 13 test executions.)*

---

## 3. Automated Test Scenarios Explained

| Test Name | What It Verifies |
|---|---|
| `McpEndpoint_WithoutBearerToken_Returns401Unauthorized` | Calling `POST /mcp` without an `Authorization: Bearer <token>` header immediately yields HTTP 401. |
| `McpClient_ListTools_DiscoversOnlyExpectedReadOnlyToolsWithoutTenantParams` | Standalone `McpClient` discovers exactly `find_titles_missing_metadata` and `get_title_details`. Confirms `ReadOnly = true` and verifies zero `userId` or tenant parameters in the JSON schemas. |
| `McpClient_FindTitlesMissingMetadata_ReturnsIncompleteUserABooksAndRespectsFilters` | Calls `find_titles_missing_metadata` as User A. Asserts it returns only the 3 incomplete titles for User A, ignores complete titles, and excludes User B's book. Verifies format filter (`Audiobook`) and field filter (`SeriesNumber`). |
| `McpClient_FindTitlesMissingMetadata_PaginatesCorrectlyWithHasMore` | Verifies bounded pagination (`page=1, pageSize=2` returns 2 items and `hasMore: true`; `page=2, pageSize=2` returns 1 item and `hasMore: false`). |
| `McpClient_GetTitleDetails_ReturnsFullBookEditionsAndPlatformSummary` | Calls `get_title_details` as User A on their audiobook title. Verifies book metadata, edition metadata, format completeness flags, and Kindle Direct platform publication summary. |
| `McpClient_GetTitleDetails_TwoUserIsolation_MasksOtherUsersBook` | **Security Invariant:** User A calls `get_title_details` with User B's `bookId`. Proves the server returns an identical `NOT_FOUND_OR_UNAUTHORIZED` error as a non-existent ID (999999), preventing cross-tenant existence enumeration. Proves User B can read their own book. |
| `McpClient_InvalidArguments_ReturnsActionableErrors` | Theory testing invalid parameters: `page: 0`, `pageSize: 51`, `format: "Laserdisc"`, `format: "1"`, `missingField: "Bogus"`, and `bookId: 0`. Asserts all return actionable `INVALID_ARGUMENT:` errors. |
| `RestEndpoints_MissingMetadataAndDetails_MatchMcpContractsAndIsolation` | Confirms identical behavior and status codes (`200`, `400`, `401`, `404`) across the REST routes (`/api/books/missing-metadata` and `/api/books/{id}/details`). |

---

## 4. Manual Verification Against Live API (`curl` / PowerShell)

To test a live running instance of `PublishingTracker.Api`:

### Step 1: Start the API
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project Publishing-Tracker/apps/api/src/PublishingTracker.Api
```
*(Default launch profile listens at `https://localhost:7285` / `http://localhost:5226`)*

### Step 2: Acquire a JWT Token
If testing against an existing database seeded with test accounts:
```bash
curl -k -X POST https://localhost:7285/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"author-a@example.test","password":"Password123!"}'
```
*(Tip: If testing against a fresh, empty development database, first register a user via `POST /api/auth/register` with `{"email":"author-a@example.test","password":"Password123!","firstName":"Author","lastName":"Alpha"}`)*

Set the returned token to a variable:
```powershell
$token = "<token_from_login>"
```

### Step 3: MCP Handshake (`initialize`)
Send a standard JSON-RPC 2.0 `initialize` request to `POST /mcp`:

```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2025-06-18",
      "capabilities": {},
      "clientInfo": { "name": "manual-tester", "version": "1.0" }
    }
  }'
```

**Expected Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2025-06-18",
    "capabilities": { "tools": { "listChanged": false } },
    "serverInfo": { "name": "PublishingTracker.Api", "version": "1.0.0" }
  }
}
```

### Step 4: Discover Tools (`tools/list`)
```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }'
```

**Verification Checklist:**
- [ ] Exactly two tools appear: `find_titles_missing_metadata` and `get_title_details`.
- [ ] `readOnly: true` is present on both tool descriptions.
- [ ] No `userId`, `ownerId`, or `callerUserId` appears in `inputSchema.properties`.

### Step 5: Execute `find_titles_missing_metadata`
```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "find_titles_missing_metadata",
      "arguments": {
        "format": "Audiobook"
      }
    }
  }'
```

**Expected Result:** Returns structured JSON containing incomplete audiobooks with `missingFields: ["Narrator", "Duration"]`.

### Step 6: Verify Two-User Security Isolation
1. Obtain an ID for a book owned by User B (e.g. ID `99`).
2. Make a `tools/call` for `get_title_details` with `"bookId": 99` using User A's token:

```bash
curl -k -X POST https://localhost:7285/mcp \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{
    "jsonrpc": "2.0",
    "id": 4,
    "method": "tools/call",
    "params": {
      "name": "get_title_details",
      "arguments": {
        "bookId": 99
      }
    }
  }'
```

**Expected Result:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "isError": true,
    "content": [
      {
        "type": "text",
        "text": "NOT_FOUND_OR_UNAUTHORIZED: Title was not found or is not accessible."
      }
    ]
  }
}
```
*(Notice: The response masks the book's existence and leaks zero title or author details.)*

---

## 5. Troubleshooting & Diagnostics

| Symptom | Root Cause | Fix |
|---|---|---|
| `HTTP 401 Unauthorized` | Missing or expired JWT, or token missing `Bearer ` prefix. | Re-authenticate at `/api/auth/login`. Ensure the header format is `Authorization: Bearer <token>`. |
| `HTTP 400 Bad Request` | Required Streamable HTTP headers missing. | Include `-H "Accept: application/json, text/event-stream"` on the request. |
| `INVALID_ARGUMENT: Unsupported format '...'` | Passing numeric format (`"1"`) or comma lists (`"Ebook,Paperback"`). | Pass exact declared format name: `Ebook`, `Paperback`, `Hardcover`, `Audiobook`, or `LargeText`. |
| Tool returns text instead of JSON | Client omitting structured content handling. | Ensure client supports `UseStructuredContent = true` (MCP Protocol 2025-06-18+). |
| Handshake hangs or fails | Trying to connect over stdio rather than HTTP. | Point client at `https://localhost:7285/mcp` using `StreamableHttp` transport mode. |
