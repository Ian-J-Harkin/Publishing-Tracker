# PublishingTracker MCP & Agent — User & Integration Guide

This guide describes how to connect to, authenticate with, and use the **PublishingTracker Model Context Protocol (MCP)** server hosted at `/mcp` inside `PublishingTracker.Api`, as well as the **PublishingTracker.Agent** query API (`POST /api/agent/query`) and the **PublishingTracker.Rag** retrieval capabilities (Steps 4.1–4.5 implemented).

---

## 1. Overview & Architecture

The PublishingTracker MCP server exposes authenticated, read-only catalogue querying capabilities to AI agents, IDEs, desktop clients, and `PublishingTracker.Agent` via the open **Model Context Protocol (MCP)** specification. In addition, the integrated RAG module enables semantic guideline retrieval over markdown guides and caller notes.

```
┌─────────────────────────────────┐      ┌───────────────────────────────────────────┐
│ IDE / Desktop MCP Client        │      │ End User / API Client                     │
│ (Claude Desktop, Cursor, CLI)   │      └─────────────────────┬─────────────────────┘
└────────────────┬────────────────┘                            │ POST /api/agent/query
                 │                                             │ Header: Authorization: Bearer <jwt>
                 │                                             ▼
                 │                        ┌───────────────────────────────────────────┐
                 │                        │ PublishingTracker.Agent (Phases 2–4)      │
                 │                        │   ├── JwtBearerEvents (identity + iat)    │
                 │                        │   ├── Two-Stage AgentLoopRunner           │
                 │                        │   ├── CompositeToolProvider:              │
                 │                        │   │     ├── StreamableHttpMcpToolProvider │
                 │                        │   │     └── RagSearchToolFactory (RAG)    │
                 │                        │   ├── EvidenceLedger + Validator          │
                 │                        │   └── PerRequestBearerHandler             │
                 │                        └────────────────────┬──────────────────────┘
                 │                                             │
                 └──────────────────────┬──────────────────────┘
                                        │ Streamable HTTP (POST /mcp)
                                        │ Header: Authorization: Bearer <jwt>
                                        ▼
┌────────────────────────────────────────────────────────┐
│ PublishingTracker.Api                                  │
│   ├── app.UseAuthentication()                          │
│   ├── app.UseAuthorization()                           │
│   ├── app.UseRoleValidation()                          │
│   │                                                    │
│   └── app.MapMcp("/mcp").RequireAuthorization()        │
│         │                                              │
│         ▼                                              │
│       PublishingTracker.Mcp (CatalogueMcpTools)        │
│         ├── find_titles_missing_metadata               │
│         └── get_title_details                          │
└────────────────────────────────────────────────────────┘
```

### Key Architectural Invariants
1. **Hosted Streamable HTTP:** The server operates as an HTTP endpoint at `/mcp` (`ModelContextProtocol.AspNetCore` 1.3.0). It requires no local subprocess or `stdio` pipe, enabling remote deployment and standard HTTP auth.
2. **Stateless HTTP Request Turns:** Each MCP turn executes inside its own ASP.NET Core request scope (`options.Stateless = true`).
3. **Strict Non-Model-Controlled Tenant Identity:** Tool schemas **never** accept a `userId` or tenant parameter. Identity is derived strictly from the caller's validated JWT in server-side code (`ICurrentUserService.UserId`).
4. **Read-Only Guarantees:** All tools are decorated with `ReadOnly = true`, `Idempotent = true`, and `Destructive = false`. No MCP tool can alter database records.

---

## 2. Authentication & JWT Tokens

Every request to `/mcp` must present a valid Bearer JWT issued by `PublishingTracker.Api`.

### Obtaining a Token
Authenticate against the API auth endpoint:

```bash
curl -X POST https://localhost:7285/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "author-a@example.test",
    "password": "Password123!"
  }'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": 1,
    "email": "author-a@example.test",
    "fullName": "Author Alpha",
    "role": "User"
  }
}
```

### Token Invariants
- **Expiry:** Tokens expire after **60 minutes** (`DateTime.UtcNow.AddMinutes(60)`).
- **`iat` Claim:** Tokens contain the `iat` (issued-at) claim, enforced by `RoleValidationMiddleware` to immediately invalidate tokens if user roles or permissions change.
- **Unauthenticated Handling:** Any request to `/mcp` without a valid token immediately receives `401 Unauthorized`.

---

## 3. Available Tools & Contracts

### 3.1 `find_titles_missing_metadata`

Identifies titles and specific editions owned by the authenticated caller that lack required publishing metadata fields.

#### Parameters
| Argument | Type | Default | Description |
|---|---|---|---|
| `format` | `string?` | `null` | Optional edition format filter: `Ebook`, `Paperback`, `Hardcover`, `Audiobook`, or `LargeText`. |
| `missingField` | `string?` | `null` | Optional missing field filter: `Description`, `Genre`, `CoverImageUrl`, `Editions`, `SeriesNumber`, `PublicationDate`, `BasePrice`, `ISBN`, `PageCount`, `Dimensions`, `ISBN_Or_PlatformProductId`, `FileSize`, `Narrator`, `Duration`. |
| `page` | `int` | `1` | 1-based page index (must be $\ge 1$). |
| `pageSize` | `int` | `20` | Results per page (bounded between $1$ and $50$). |

#### Format Completeness Rules Applied
- **Book-Level:** `Description`, `Genre`, `CoverImageUrl`, `Editions` (must have at least one edition). If `SeriesName` is populated, `SeriesNumber` is also required.
- **Print Formats (`Paperback`, `Hardcover`, `LargeText`):** Require `ISBN`, `PublicationDate`, `BasePrice` ($\ge 0$), `PageCount`, and `Dimensions`.
- **Ebook:** Requires `PublicationDate`, `BasePrice` ($\ge 0$), `FileSize`, and either an `ISBN` or a platform product identifier (e.g., Amazon ASIN).
- **Audiobook:** Requires `PublicationDate`, `BasePrice` ($\ge 0$), `Narrator`, `Duration`, and either an `ISBN` or a platform product identifier.

#### Return Schema (`MissingMetadataPageDto`)
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "hasMore": false,
  "items": [
    {
      "bookId": 42,
      "title": "Audiobook in Production",
      "missingBookFields": [],
      "editions": [
        {
          "editionId": 105,
          "format": "Audiobook",
          "missingFields": [
            "Narrator",
            "Duration"
          ]
        }
      ]
    }
  ]
}
```

---

### 3.2 `get_title_details`

Fetches complete book metadata, all editions, format completeness flags, and platform distribution summaries for a single title.

#### Parameters
| Argument | Type | Required | Description |
|---|---|---|---|
| `bookId` | `int` | Yes | Positive integer Book ID owned by the caller. |

#### Return Schema (`TitleDetailsDto`)
```json
{
  "bookId": 42,
  "title": "Audiobook in Production",
  "author": "Author Alpha",
  "seriesName": "Alpha Chronicles",
  "seriesNumber": 1,
  "genre": "Sci-Fi",
  "description": "A thrilling space adventure.",
  "coverImageUrl": "https://example.test/covers/42.jpg",
  "createdAt": "2026-11-17T20:00:00Z",
  "updatedAt": "2026-11-17T20:00:00Z",
  "missingBookFields": [],
  "editions": [
    {
      "editionId": 105,
      "format": "Audiobook",
      "isbn": null,
      "publicationDate": "2026-12-01T00:00:00Z",
      "basePrice": 19.99,
      "pageCount": null,
      "dimensions": null,
      "fileSize": null,
      "narrator": null,
      "duration": null,
      "missingFields": [
        "Narrator",
        "Duration"
      ],
      "platformSummaries": [
        {
          "platformId": 1,
          "platformName": "Kindle Direct",
          "isPublished": false,
          "publishedDate": null,
          "platformProductId": "B0AUDIO0001"
        }
      ]
    }
  ]
}
```

#### Security & Masking Invariant
If a caller passes a `bookId` belonging to another user, the server returns the **exact same error** as if the ID did not exist:
```
NOT_FOUND_OR_UNAUTHORIZED: Title was not found or is not accessible.
```
This guarantees an attacker cannot probe for valid IDs or enumerate catalogue contents across tenants.

---

## 4. Connecting with MCP Clients

### 4.1 Claude Desktop Configuration
In Claude Desktop's `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "publishing-tracker": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote-client",
        "https://localhost:7285/mcp",
        "--header",
        "Authorization: Bearer <YOUR_JWT_TOKEN>"
      ]
    }
  }
}
```

*(Note: Replace `<YOUR_JWT_TOKEN>` with your active JWT token obtained via `/api/auth/login`. In local development, ensure your local ASP.NET Core dev certificate is trusted via `dotnet dev-certs https --trust`.)*

### 4.2 Cursor / VS Code MCP Configuration
In your project `.cursor/mcp.json` or VS Code MCP settings:

```json
{
  "mcpServers": {
    "publishing-tracker": {
      "url": "https://localhost:7285/mcp",
      "headers": {
        "Authorization": "Bearer <YOUR_JWT_TOKEN>"
      }
    }
  }
}
```

### 4.3 Programmatic C# Client (`ModelContextProtocol.Client`)
```csharp
using System.Net.Http.Headers;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7285") };
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

var transport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri(httpClient.BaseAddress, "/mcp"),
        TransportMode = HttpTransportMode.StreamableHttp
    },
    httpClient,
    ownsHttpClient: true);

await using var mcpClient = await McpClient.CreateAsync(transport);

// Discover available tools
var tools = await mcpClient.ListToolsAsync();

// Call find_titles_missing_metadata
var result = await mcpClient.CallToolAsync(
    "find_titles_missing_metadata",
    new Dictionary<string, object?> { ["format"] = "Audiobook" });
```

---

## 5. REST Route Parity

Both MCP tools are also exposed as standard REST endpoints on `PublishingTracker.Api`:

| MCP Tool | Corresponding REST Endpoint |
|---|---|
| `find_titles_missing_metadata` | `GET /api/books/missing-metadata?format={format}&missingField={field}&page={p}&pageSize={s}` |
| `get_title_details` | `GET /api/books/{id:int}/details` |

Both interfaces enforce identical authentication, input validation rules, and error formatting.

---

## 6. Using the Agent Host (`PublishingTracker.Agent` — Phases 2 & 3)

`PublishingTracker.Agent` provides a natural-language question-answering endpoint (`POST /api/agent/query`) backed by `Microsoft.Extensions.AI`, an explicit two-stage orchestration loop, a per-run `EvidenceLedger`, and strict output validation.

### 6.1 Host Configuration (`AIProvider` & `AgentLoop`)

Configure `PublishingTracker.Agent` using user secrets, environment variables, or configuration files:

```json
{
  "Jwt": {
    "Key": "<same-signing-key-as-PublishingTracker.Api>",
    "Issuer": "PublishingTracker.Api",
    "Audience": "PublishingTracker.Api"
  },
  "AIProvider": {
    "Type": "openrouter",
    "ModelId": "anthropic/claude-sonnet-4",
    "Endpoint": "https://openrouter.ai/api/v1",
    "McpEndpoint": "https://localhost:7285/mcp"
  },
  "AgentLoop": {
    "MaxModelTurns": 6,
    "MaxTotalToolCalls": 8,
    "MaxValidationRetries": 2,
    "RepeatCallLimit": 2,
    "MaxPaginationPages": 2,
    "MaxToolPayloadBytes": 16384,
    "PerCallTimeout": "00:00:20",
    "OverallTimeout": "00:01:00"
  }
}
```

- **Supported `AIProvider:Type` values:** `"openrouter"` (default), `"openai"`, and `"ollama"` (case-insensitive; unrecognized values fail fast at startup). Supply secrets via `OPENROUTER_API_KEY`, `OPENAI_API_KEY`, or user secrets rather than committed config files; `AIProviderOptions` marks secrets `[JsonIgnore]` and outputs `[REDACTED]` in `ToString()`.
- **OpenRouter Privacy Headers:** Outbound requests for `"openrouter"` automatically attach `HTTP-Referer: https://publishingtracker.local`, `X-Title: PublishingTracker`, and `data_collection: deny`, and wrap `IChatClient` with OpenTelemetry tracing (`EnableSensitiveData = false`).
- **Trusted MCP Endpoint:** `McpEndpoint` must point to an absolute `/mcp` URL over HTTPS (loopback HTTP is permitted only in `Development` or `Testing`).

### 6.2 Authentication Requirements at `/api/agent/query`

1. Obtain a JWT from `PublishingTracker.Api` (`/api/auth/login`).
2. `PublishingTracker.Agent`'s `JwtBearerEvents.OnTokenValidated` validates the signature, issuer, audience, and expiry, **and** requires both a non-empty user identity claim (`NameIdentifier` / `nameid` / `sub`) and a positive integer `iat` claim before invoking any LLM turns.
3. On outbound calls to `/mcp`, `PerRequestBearerHandler` rejects multi-value `Authorization` headers or tokens containing whitespace, strips any prior header, and attaches the caller's validated token strictly per `HttpRequestMessage`.

### 6.3 Calling `POST /api/agent/query`

Since `PublishingTracker.Agent` does not define a default `launchSettings.json` URL, start the host with an explicit URL (e.g., `--urls https://localhost:7290`) and send an authenticated request:

```bash
curl -k -X POST https://localhost:7290/api/agent/query \
  -H "Authorization: Bearer $token" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Which audiobooks are missing metadata, and what fields do they need?"
  }'
```

**Version `1.0` Response Envelope (`AgentQueryResponse`):**

```json
{
  "schemaVersion": "1.0",
  "status": "answered",
  "answer": "Audiobook 'Audio Missing Narrator' (BookId 42, EditionId 105) is missing Narrator and Duration.",
  "evidence": {
    "catalogueSources": [
      {
        "bookId": 42,
        "editionId": 105,
        "fields": ["Narrator", "Duration"],
        "claim": "Edition 105 is missing Narrator and Duration."
      }
    ],
    "documentSources": []
  },
  "toolsUsed": [
    {
      "toolName": "find_titles_missing_metadata",
      "turn": 1,
      "durationMs": 38,
      "succeeded": true
    },
    {
      "toolName": "get_title_details",
      "turn": 2,
      "durationMs": 29,
      "succeeded": true
    }
  ],
  "turns": 3,
  "totalToolCalls": 2,
  "limitations": null,
  "error": null
}
```

- `toolsUsed` is always populated by the Agent Host from observed tool executions—never copied from model output.
- If pagination truncation occurs (`hasMore == true`), `limitations` must disclose truncation and the host guarantees `Catalogue results were truncated at the page limit (hasMore=true); additional matching titles exist.` is present.

### 6.4 Provenance Boundary & Closed Public Error Codes

> **Provenance Boundary (`ADR-007`):** Validation guarantees **structural source and field provenance** against the run's `EvidenceLedger`—meaning every cited `bookId`, `(bookId, editionId)` pair, cited field name, and `(documentId, chunkId)` pair was genuinely returned in tool results during that run. Host validation does **not** perform natural-language semantic entailment on free-text `answer` or `claim` prose.

Failures always return `status: "could_not_answer"`, empty `evidence`, host-recorded `toolsUsed`, and an approved public `error` prefixed by one of 18 closed `AgentErrorCode` values (internal stack traces and secrets stay strictly in host logs):

| Category | Error Codes |
|---|---|
| **Cancellation & Timeouts** | `CANCELLED`, `TIMEOUT` |
| **Setup, Model & Turns** | `TOOL_DISCOVERY_ERROR`, `INVALID_TOOLSET`, `MODEL_ERROR`, `EMPTY_RESPONSE`, `TURN_LIMIT_EXCEEDED` |
| **Budgets, Tools & Paging** | `BUDGET_EXCEEDED`, `UNKNOWN_TOOL`, `INVALID_TOOL_ARGUMENTS`, `REPEAT_CALL_LIMIT`, `PAGINATION_LIMIT_EXCEEDED`, `PAGINATION_SEQUENCE_ERROR` |
| **Payloads, Ledger & Output** | `TOOL_ERROR`, `SERIALIZATION_ERROR`, `PAYLOAD_LIMIT_EXCEEDED`, `EVIDENCE_INDEXING_ERROR`, `VALIDATION_RETRIES_EXHAUSTED` |

---

## 7. Phase 4 RAG Capabilities (`PublishingTracker.Rag`)

Phase 4 (specification detailed at [Phase-4-RAG-Module-with-pgvector.md](Phase-4-RAG-Module-with-pgvector.md); Steps 4.1–4.5 implemented) extends the Agent Host with semantic document retrieval over publishing guidelines and user notes:

- **Host-Bound Tool:** `search_publishing_guidelines(string query, int topK = 5)` searches shared repo guides (`Publishing-Tracker/apps/api/docs/rag-corpus/`, `OwnerUserId = null`) and private caller notes (`OwnerUserId = authenticatedUserId`), embedded with `openai/text-embedding-3-small` (`1536` dimensions) and queried via PostgreSQL `pgvector` `<=>` cosine distance (or in-memory cosine similarity fallback in test environments).
- **Strict Tenant Binding (`ADR-009`):** `authenticatedUserId` is resolved strictly from the caller's validated JWT in host code and never appears in tool parameters or schemas.
- **Composite Tooling:** The agent dynamically presents both MCP catalogue tools (`find_titles_missing_metadata`, `get_title_details`) and the in-process RAG tool (`search_publishing_guidelines`) to the model via `CompositeToolProvider`.
- **Linked Citations & Verification:** Retrieved chunks populate `evidence.documentSources` (`documentId`, `chunkId`, `claim`) and are cross-verified by `AgentOutputValidator` against `EvidenceLedger` (Step 4.6).
