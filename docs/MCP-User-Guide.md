# PublishingTracker MCP Server — User & Integration Guide

This guide describes how to connect to, authenticate with, and use the **PublishingTracker Model Context Protocol (MCP)** server hosted at `/mcp` inside `PublishingTracker.Api`.

---

## 1. Overview & Architecture

The PublishingTracker MCP server exposes authenticated, read-only catalogue querying capabilities to AI agents, IDEs, and desktop clients via the open **Model Context Protocol (MCP)** specification.

```
┌─────────────────────────────────┐
│ MCP Client (Claude Desktop,     │
│ Cursor, Agent, or Custom CLI)   │
└────────────────┬────────────────┘
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
