# Phase 2 — Expanded Design: Agent Loop, Dual Budgets, and Basic Tracing

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md), and [Phase-1-Read-Only-Catalogue-and-MCP.md](Phase-1-Read-Only-Catalogue-and-MCP.md).

---

## 1. Purpose, Scope, and System Boundaries

### Goal
Implement `PublishingTracker.Agent` as an authenticated ASP.NET Core service hosting an **explicit, dual-budget orchestration loop** powered by `Microsoft.Extensions.AI` (`IChatClient`). Connect to `PublishingTracker.Api`'s `/mcp` endpoint over Streamable HTTP using per-request JWT forwarding (never shared `DefaultRequestHeaders`), enforce deterministic execution controls (`MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, payload caps, recursive JSON canonicalization for repeat-call limits, 2-page pagination boundaries), emit privacy-safe OpenTelemetry activity traces, and verify behavior with 18 deterministic test doubles (`ScriptedChatClient` and `RecordingHttpMessageHandler`).

```
                           ┌────────────────────────────────────────────────────────┐
                           │               PublishingTracker.Agent                  │
                           │                                                        │
Client (JWT) ─────────────>│ POST /api/agent/query                                  │
                           │   │                                                    │
                           │   ▼                                                    │
                           │ AgentLoopRunner (Dual Budgets & Tracing)               │
                           │   │                                                    │
                           │   ├── [1] Call IChatClient (OpenRouter / Claude)       │
                           │   │         (Model tool-selection turn)                │
                           │   │                                                    │
                           │   └── [2] Execute MCP Tool via PerRequestBearerHandler  │
                           │         │   (Attaches caller JWT to request)           │
                           │         ▼                                              │
                           └─────────┼──────────────────────────────────────────────┘
                                     │ Streamable HTTP (POST /mcp)
                                     ▼
                           ┌────────────────────────────────────────────────────────┐
                           │               PublishingTracker.Api                    │
                           │                                                        │
                           │ app.MapMcp("/mcp").RequireAuthorization()              │
                           │   │                                                    │
                           │   ▼                                                    │
                           │ CatalogueMcpTools (Phase 1: find / get_details)        │
                           └────────────────────────────────────────────────────────┘
```

### In Scope for Phase 2
1. **New project `src/PublishingTracker.Agent/PublishingTracker.Agent.csproj`**: ASP.NET Core Web API on `.NET 10 LTS` (`net10.0`), referenced by the solution and configured with JWT authentication matching `PublishingTracker.Api`.
2. **Configuration & Provider Setup (`AIProviderOptions` / `AIProviderSetup`)**: Ported and hardened from `ai-code-review-bot`, supporting OpenRouter (primary, pinned Claude model) + Ollama and OpenAI fallbacks (with Azure OpenAI explicitly deferred to keep provider setup lightweight and testable), with `ValidateOnStart()`, `[JsonIgnore]` on secrets, redacted `ToString()`, and OpenRouter privacy headers (`HTTP-Referer`, `X-Title`, and `data_collection: "deny"`).
3. **Outbound MCP Client Transport with Per-Request JWT Propagation**: `PerRequestBearerHandler` delegating handler that validates the target MCP endpoint scheme/host, sanitizes pre-existing headers, and extracts the incoming caller JWT from `IHttpContextAccessor` to attach per `HttpRequestMessage` to `/mcp`, ensuring multi-tenant isolation across the HTTP boundary.
4. **Explicit Dual-Budget Agent Loop (`AgentLoopRunner` & `AgentLoopOptions`)**:
   - `MaxModelTurns = 6`
   - `MaxTotalToolCalls = 8` (enforced across the entire run, counting every function call returned by model turns)
   - `MaxToolPayloadBytes = 16_384` (16 KB tool response truncation / guard)
   - `RepeatCallLimit = 2` (normalized call signature: tool name + recursively canonicalized, type-preserving JSON arguments)
   - `PerCallTimeout = 20s`, `OverallTimeout = 60s`
   - 2-page pagination cap with mandatory `limitations` note when `hasMore` remains `true`
   - Startup option validation (`ValidateOnStart()`) rejecting non-positive limits and contradictory timeouts.
   - Run-level timeout and cancellation differentiation (caller abort vs. overall deadline vs. per-call model timeout vs. tool timeout).
   - Non-cooperative tool timeout protection via `Task.WhenAny`.
   - Tool duplication, serialization, and empty-response handling.
   - Generic public error codes (`TOOL_ERROR`, `TIMEOUT`, `BUDGET_EXCEEDED`) masking internal exceptions from client payloads.
5. **Observability & OpenTelemetry (`ActivitySource("PublishingTracker.Agent")`)**: Root span `agent.run` and child spans `agent.model_turn` and `agent.tool_call` with execution metadata (turn numbers, tool names, durations, payload sizes, status codes, error codes), strictly excluding sensitive data (no prompts, tool arguments, full tool outputs, or bearer tokens).
6. **Agent Query Endpoint**: `POST /api/agent/query` accepting `AgentQueryRequest` and returning `AgentQueryResponse`.
7. **Test Suite (`PublishingTracker.Agent.Tests`)**: 18 deterministic unit tests with `ScriptedChatClient` (simulating tool turns and completions without external API calls) and `RecordingHttpMessageHandler` (validating provider configuration, headers, and endpoints without spend).

### Out of Scope for Phase 2
- **Evidence Ledger & Stage 2 Structured Output Verification** (Phase 3: `EvidenceLedger`, `AgentOutputValidator`, citation provenance, and JSON schema repair turns).
- **RAG & `pgvector` Document Retrieval** (Phase 4: `PublishingTracker.Rag` and `search_publishing_guidelines`).
- **Azure OpenAI Dedicated Client Integration** (Deferred to future deployment milestone; Phase 2 focuses on OpenRouter, OpenAI, and Ollama).
- **Interactive Chat UI** (Deferred until after Phase 6).

---

## 2. Verified Packages & SDK Primitives (`.NET 10`)

The following packages are pinned in `Directory.Packages.props` and cached locally on `.NET 10`:

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Extensions.AI` | `10.10.0` | Core abstractions: `IChatClient`, `ChatMessage`, `ChatOptions`, `AIFunction`, `FunctionCallContent`, `FunctionResultContent` |
| `Microsoft.Extensions.AI.OpenAI` | `10.10.0` | Adapts OpenAI/OpenRouter REST clients to `IChatClient` (`.AsChatClient()`) |
| `ModelContextProtocol` | `1.3.0` | Client MCP SDK: `McpClient`, `HttpClientTransport`, and `McpClientTool` (`AIFunction` interop) |
| `OpenTelemetry` | `1.16.0` | Activity listener and telemetry export primitives |
| `OpenTelemetry.Extensions.Hosting` | `1.16.0` | ASP.NET Core OpenTelemetry hosting integration |

### SDK Interoperability Note: MCP to `Microsoft.Extensions.AI`
`ModelContextProtocol` 1.3.0 tools (`McpClientTool`) derive directly from `Microsoft.Extensions.AI.AIFunction`. When `mcpClient.ListToolsAsync()` is called, each tool can be passed straight into `ChatOptions.Tools` for `IChatClient` without custom adapter classes or reflection wrappers.

---

## 3. Workstream 1 — Project Architecture & Configuration

### 3.1 Project Definition: `src/PublishingTracker.Agent/PublishingTracker.Agent.csproj`
Target framework: `net10.0`. References:
- `PublishingTracker.Domain`
- `PublishingTracker.Application` (for shared DTOs/constants)
- NuGet packages: `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `ModelContextProtocol`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `OpenTelemetry.Extensions.Hosting`.

### 3.2 Provider Options: `AIProviderOptions.cs`
Pattern ported and hardened from `ai-code-review-bot`:

```csharp
namespace PublishingTracker.Agent.Configuration;

public sealed class AIProviderOptions
{
    public const string SectionName = "AIProvider";

    public static readonly string[] SupportedTypes = ["openrouter", "openai", "ollama"];

    /// <summary>Provider type: "openrouter" (default), "openai", or "ollama".</summary>
    public string Type { get; set; } = "openrouter";

    /// <summary>Active model ID (e.g. "anthropic/claude-sonnet-4").</summary>
    public string ModelId { get; set; } = "anthropic/claude-sonnet-4";

    /// <summary>Base endpoint URL.</summary>
    public string? Endpoint { get; set; } = "https://openrouter.ai/api/v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    public string OpenRouterModelId { get; set; } = "anthropic/claude-sonnet-4";
    public string? OpenRouterEndpoint { get; set; } = "https://openrouter.ai/api/v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string OpenRouterApiKey { get; set; } = string.Empty;

    public string OllamaModelId { get; set; } = "qwen2.5-coder:7b";
    public string? OllamaEndpoint { get; set; } = "http://localhost:11434/v1";

    /// <summary>Target PublishingTracker.Api MCP endpoint.</summary>
    public string McpEndpoint { get; set; } = "https://localhost:7285/mcp";

    public override string ToString() =>
        $"AIProviderOptions {{ Type = {Type}, ModelId = {ModelId}, Endpoint = {Endpoint}, ApiKey = {Redact(ApiKey)}, McpEndpoint = {McpEndpoint} }}";

    private static string Redact(string? val) => string.IsNullOrWhiteSpace(val) ? "<empty>" : "[REDACTED]";
}
```

### 3.3 Provider Resolution & Client Factory: `AIProviderSetup.cs`
Resolves options with explicit provider type validation, environment variable fallbacks (`OPENROUTER_API_KEY`, `OPENAI_API_KEY`, `OPENROUTER_MODEL_NAME`, `OPENAI_MODEL_NAME`), wires `customHttpClient` to `OpenAIClientOptions.Transport`, configures outbound headers for OpenRouter privacy compliance (`data_collection: "deny"`, `X-Title: PublishingTracker`), and constructs an `IChatClient`:

```csharp
namespace PublishingTracker.Agent.Configuration;

public static class AIProviderSetup
{
    public const string OpenRouterDefaultEndpoint = "https://openrouter.ai/api/v1";
    public const string OpenRouterDefaultModelId = "anthropic/claude-sonnet-4";
    public const string OpenAiDefaultEndpoint = "https://api.openai.com/v1";
    public const string OpenAiDefaultModelId = "gpt-4o-mini";
    public const string OllamaDefaultEndpoint = "http://localhost:11434/v1";
    public const string OllamaDefaultModelId = "qwen2.5-coder:7b";

    public static AIProviderOptions Resolve(AIProviderOptions configured, Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(getEnv);

        var rawType = configured.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AIProviderOptions.SupportedTypes.Contains(rawType))
        {
            throw new InvalidOperationException(
                $"Unsupported AIProvider:Type '{configured.Type}'. Supported values: {string.Join(", ", AIProviderOptions.SupportedTypes)}.");
        }

        if (rawType == "openrouter")
        {
            var key = FirstNonEmpty(getEnv("OPENROUTER_API_KEY"), configured.OpenRouterApiKey, configured.ApiKey);
            var model = FirstNonEmpty(getEnv("OPENROUTER_MODEL_NAME"), configured.OpenRouterModelId, configured.ModelId, OpenRouterDefaultModelId);
            var endpoint = FirstNonEmpty(configured.OpenRouterEndpoint, configured.Endpoint, OpenRouterDefaultEndpoint);

            return new AIProviderOptions
            {
                Type = "openrouter",
                ModelId = model!,
                Endpoint = endpoint,
                ApiKey = key ?? string.Empty,
                McpEndpoint = configured.McpEndpoint
            };
        }

        if (rawType == "ollama")
        {
            return new AIProviderOptions
            {
                Type = "ollama",
                ModelId = FirstNonEmpty(configured.OllamaModelId, configured.ModelId, OllamaDefaultModelId)!,
                Endpoint = FirstNonEmpty(configured.OllamaEndpoint, configured.Endpoint, OllamaDefaultEndpoint),
                ApiKey = FirstNonEmpty(configured.ApiKey, "ollama")!,
                McpEndpoint = configured.McpEndpoint
            };
        }

        // openai
        var openAiKey = FirstNonEmpty(getEnv("OPENAI_API_KEY"), configured.ApiKey) ?? string.Empty;
        var openAiModel = FirstNonEmpty(getEnv("OPENAI_MODEL_NAME"), configured.ModelId, OpenAiDefaultModelId)!;
        var openAiEndpoint = FirstNonEmpty(configured.Endpoint, OpenAiDefaultEndpoint);

        return new AIProviderOptions
        {
            Type = "openai",
            ModelId = openAiModel,
            Endpoint = openAiEndpoint,
            ApiKey = openAiKey,
            McpEndpoint = configured.McpEndpoint
        };
    }

    public static IChatClient CreateChatClient(
        AIProviderOptions options,
        HttpClient? customHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Type != "ollama" && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException($"API key is required for provider '{options.Type}'. Set OPENROUTER_API_KEY or AIProvider:ApiKey.");
        }

        var openAiOptions = new OpenAI.OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            openAiOptions.Endpoint = new Uri(options.Endpoint);
        }

        // Wire customHttpClient for test doubles (RecordingHttpMessageHandler)
        if (customHttpClient is not null)
        {
            openAiOptions.Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(customHttpClient);
        }

        if (options.Type == "openrouter")
        {
            // Privacy & attribution headers required by ADR-008
            openAiOptions.AddPolicy(new OpenRouterHeadersPolicy(), System.ClientModel.Primitives.ClientPipelinePosition.PerCall);
        }

        var client = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(options.ApiKey), openAiOptions);
        return client.GetChatClient(options.ModelId).AsChatClient();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
```

---

## 4. Workstream 2 — Auth Propagation & Outbound MCP Client

### 4.1 Per-Request JWT Propagation & Credential Guard: `PerRequestBearerHandler.cs`
Enforces `ADR-005` with strict credential protection:
1. **Endpoint Trust Verification:** Ensures the target request URI uses `https://` (or `http://localhost` / `http://127.0.0.1` strictly in Development/Testing) and the path begins with the expected MCP route (`/mcp`), preventing credential leakage to arbitrary endpoints.
2. **Conflict Sanitization:** Explicitly removes any pre-existing `Authorization` header on the outgoing message before attaching the caller's validated token.
3. **Fail-Closed Security:** Rejects unauthenticated callers or malformed bearer headers immediately with `UnauthorizedAccessException`.

```csharp
namespace PublishingTracker.Agent.Auth;

public sealed class PerRequestBearerHandler(
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ValidateDestinationSecurity(request.RequestUri);

        var context = httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("No active HttpContext for outbound MCP request.");

        if (context.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Caller must be authenticated.");

        var rawHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(rawHeader) || !rawHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Missing or invalid Bearer token.");

        // Fail-safe: remove any pre-existing Authorization header
        if (request.Headers.Contains("Authorization"))
        {
            request.Headers.Remove("Authorization");
        }

        // Attach caller's validated token per request message
        if (!request.Headers.TryAddWithoutValidation("Authorization", rawHeader))
        {
            throw new InvalidOperationException("Failed to attach Authorization header to outgoing request.");
        }

        return base.SendAsync(request, cancellationToken);
    }

    private void ValidateDestinationSecurity(Uri? uri)
    {
        if (uri is null)
            throw new InvalidOperationException("Target MCP request URI cannot be null.");

        if (!uri.IsAbsoluteUri)
            throw new InvalidOperationException("Target MCP URI must be absolute.");

        var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLocal = uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        // Require HTTPS unless loopback in Development/Testing
        if (!isHttps && !(isLocal && (environment.IsDevelopment() || environment.IsEnvironment("Testing"))))
        {
            throw new InvalidOperationException($"Insecure HTTP MCP endpoint '{uri}' is not permitted in {environment.EnvironmentName}.");
        }

        if (!uri.AbsolutePath.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Target URI path '{uri.AbsolutePath}' is not a permitted MCP endpoint.");
        }
    }
}
```

### 4.2 Outbound MCP Tool Provider: `IMcpToolProvider.cs`
Resolves discovered MCP tools from `PublishingTracker.Api/mcp` for the current HTTP turn:

```csharp
namespace PublishingTracker.Agent.Mcp;

public interface IMcpToolProvider
{
    Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken = default);
}

public sealed class StreamableHttpMcpToolProvider : IMcpToolProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri _mcpEndpoint;

    public StreamableHttpMcpToolProvider(HttpClient httpClient, IOptions<AIProviderOptions> options)
    {
        _httpClient = httpClient;
        _mcpEndpoint = new Uri(options.Value.McpEndpoint);
    }

    public async Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = _mcpEndpoint,
                TransportMode = HttpTransportMode.StreamableHttp
            },
            _httpClient,
            ownsHttpClient: false);

        await using var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

        // McpClientTool derives directly from Microsoft.Extensions.AI.AIFunction
        return tools.Cast<AIFunction>().ToList();
    }
}
```

---

## 5. Workstream 3 — Explicit Dual-Budget Loop Mechanics

### 5.1 Configuration & Options Validation: `AgentLoopOptions.cs`
Locked defaults based on `ADR-006` and project specifications, with strict startup validation:

```csharp
namespace PublishingTracker.Agent.Orchestration;

public sealed class AgentLoopOptions
{
    public const string SectionName = "AgentLoop";

    /// <summary>Maximum model interaction turns (default 6).</summary>
    public int MaxModelTurns { get; init; } = 6;

    /// <summary>Maximum cumulative tool invocations across the entire run (default 8).</summary>
    public int MaxTotalToolCalls { get; init; } = 8;

    /// <summary>Maximum validation repair retries in Stage 2 (Phase 3 default 2).</summary>
    public int MaxValidationRetries { get; init; } = 2;

    /// <summary>Maximum identical tool calls allowed before aborting loop (default 2).</summary>
    public int RepeatCallLimit { get; init; } = 2;

    /// <summary>Maximum allowed raw tool payload in bytes (default 16 KB = 16,384 bytes).</summary>
    public int MaxToolPayloadBytes { get; init; } = 16_384;

    /// <summary>Timeout for each single model or tool turn (default 20 seconds).</summary>
    public TimeSpan PerCallTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Total wall-clock request deadline for the agent run (default 60 seconds).</summary>
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum pagination pages allowed to fetch per query (default 2).</summary>
    public int MaxPaginationPages { get; init; } = 2;

    public void Validate()
    {
        if (MaxModelTurns <= 0) throw new InvalidOperationException("MaxModelTurns must be positive.");
        if (MaxTotalToolCalls <= 0) throw new InvalidOperationException("MaxTotalToolCalls must be positive.");
        if (RepeatCallLimit <= 0) throw new InvalidOperationException("RepeatCallLimit must be positive.");
        if (MaxToolPayloadBytes <= 0) throw new InvalidOperationException("MaxToolPayloadBytes must be positive.");
        if (PerCallTimeout <= TimeSpan.Zero) throw new InvalidOperationException("PerCallTimeout must be greater than zero.");
        if (OverallTimeout <= PerCallTimeout) throw new InvalidOperationException("OverallTimeout must be greater than PerCallTimeout.");
        if (MaxPaginationPages <= 0) throw new InvalidOperationException("MaxPaginationPages must be positive.");
    }
}
```

### 5.2 Loop Runner: `AgentLoopRunner.cs`
Orchestrates turns against `IChatClient`, executing tool calls step by step without automatic wrapper middleware, ensuring budgets, canonicalization, error masking, pagination contracts, and timeout isolation are strictly enforced:

```csharp
namespace PublishingTracker.Agent.Orchestration;

public sealed class AgentLoopRunner
{
    private readonly IChatClient _chatClient;
    private readonly IMcpToolProvider _toolProvider;
    private readonly AgentLoopOptions _options;
    private readonly ILogger<AgentLoopRunner> _logger;

    public AgentLoopRunner(
        IChatClient chatClient,
        IMcpToolProvider toolProvider,
        IOptions<AgentLoopOptions> options,
        ILogger<AgentLoopRunner> logger)
    {
        _chatClient = chatClient;
        _toolProvider = toolProvider;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    public async Task<AgentRunResult> RunAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var runStopwatch = Stopwatch.StartNew();
        using var runActivity = AgentTelemetry.Source.StartActivity("agent.run");
        runActivity?.SetTag("agent.max_turns", _options.MaxModelTurns);
        runActivity?.SetTag("agent.max_tool_calls", _options.MaxTotalToolCalls);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.OverallTimeout);
        var ct = linkedCts.Token;

        var toolsUsed = new List<string>();
        var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var pagesRequestedPerTool = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalToolCalls = 0;
        var limitations = new List<string>();

        // Step 1: Tool Discovery with Run-Level Deadline & Duplicate Check
        IReadOnlyList<AIFunction> tools;
        Dictionary<string, AIFunction> toolMap;
        try
        {
            tools = await _toolProvider.GetToolsAsync(ct);
            toolMap = new Dictionary<string, AIFunction>(StringComparer.Ordinal);
            foreach (var tool in tools)
            {
                if (!toolMap.TryAdd(tool.Name, tool))
                {
                    return FailRun(runActivity, runStopwatch, "INVALID_TOOLSET: Duplicate tool name detected.", toolsUsed, 0, 0);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return HandleCancellation(runActivity, runStopwatch, cancellationToken, linkedCts, toolsUsed, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool discovery failed unexpectedly.");
            return FailRun(runActivity, runStopwatch, "TOOL_DISCOVERY_ERROR: Failed to discover MCP tools.", toolsUsed, 0, 0);
        }

        var chatOptions = new ChatOptions { Tools = [.. tools] };
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AgentPrompts.SystemPromptV1),
            new(ChatRole.User, userPrompt)
        };

        // Step 2: Model & Tool Turns
        for (var turn = 1; turn <= _options.MaxModelTurns; turn++)
        {
            var turnStopwatch = Stopwatch.StartNew();
            using var turnActivity = AgentTelemetry.Source.StartActivity("agent.model_turn");
            turnActivity?.SetTag("agent.turn_number", turn);

            ChatMessage responseMessage;
            try
            {
                using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                perCallCts.CancelAfter(_options.PerCallTimeout);

                var response = await _chatClient.GetResponseAsync(messages, chatOptions, perCallCts.Token);
                responseMessage = response.Message;
                messages.Add(responseMessage);

                turnActivity?.SetTag("agent.duration_ms", turnStopwatch.ElapsedMilliseconds);
                turnActivity?.SetTag("agent.has_function_calls", responseMessage.Contents.OfType<FunctionCallContent>().Any());
            }
            catch (OperationCanceledException)
            {
                return HandleCancellation(runActivity, runStopwatch, cancellationToken, linkedCts, toolsUsed, turn, totalToolCalls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model invocation failed at turn {Turn}.", turn);
                return FailRun(runActivity, runStopwatch, "MODEL_ERROR: Call to model failed.", toolsUsed, turn, totalToolCalls);
            }

            var functionCalls = responseMessage.Contents.OfType<FunctionCallContent>().ToList();
            if (functionCalls.Count == 0)
            {
                // Reject empty or whitespace-only final completions
                if (string.IsNullOrWhiteSpace(responseMessage.Text))
                {
                    return FailRun(runActivity, runStopwatch, "EMPTY_RESPONSE: Model returned an empty response without tool calls.", toolsUsed, turn, totalToolCalls);
                }

                runActivity?.SetTag("agent.status", "completed");
                runActivity?.SetTag("agent.duration_ms", runStopwatch.ElapsedMilliseconds);
                return AgentRunResult.Completed(responseMessage.Text.Trim(), toolsUsed, turn, totalToolCalls, limitations);
            }

            // Process every tool call returned in this turn
            foreach (var call in functionCalls)
            {
                totalToolCalls++;
                runActivity?.SetTag("agent.total_tool_calls", totalToolCalls);

                if (totalToolCalls > _options.MaxTotalToolCalls)
                {
                    return FailRun(runActivity, runStopwatch,
                        $"BUDGET_EXCEEDED: Maximum tool-call budget of {_options.MaxTotalToolCalls} exceeded.",
                        toolsUsed, turn, totalToolCalls);
                }

                if (!toolMap.TryGetValue(call.Name, out var tool))
                {
                    return FailRun(runActivity, runStopwatch,
                        $"UNKNOWN_TOOL: Requested tool '{call.Name}' does not exist.",
                        toolsUsed, turn, totalToolCalls);
                }

                // Canonicalize signature with recursive type-preserving JSON
                var callSignature = CanonicalizeSignature(call.Name, call.Arguments);
                callCounts[callSignature] = callCounts.GetValueOrDefault(callSignature, 0) + 1;
                if (callCounts[callSignature] > _options.RepeatCallLimit)
                {
                    return FailRun(runActivity, runStopwatch,
                        $"REPEAT_CALL_LIMIT: Tool call '{call.Name}' with identical arguments exceeded repeat limit of {_options.RepeatCallLimit}.",
                        toolsUsed, turn, totalToolCalls);
                }

                // Enforce pagination boundaries: check if page exceeds MaxPaginationPages
                var requestedPage = ExtractPageNumber(call.Arguments);
                if (requestedPage > _options.MaxPaginationPages)
                {
                    return FailRun(runActivity, runStopwatch,
                        $"PAGINATION_LIMIT_EXCEEDED: Tool '{call.Name}' requested page {requestedPage}, exceeding maximum allowed page limit of {_options.MaxPaginationPages}.",
                        toolsUsed, turn, totalToolCalls);
                }
                pagesRequestedPerTool[call.Name] = Math.Max(pagesRequestedPerTool.GetValueOrDefault(call.Name, 0), requestedPage);

                if (!toolsUsed.Contains(call.Name))
                    toolsUsed.Add(call.Name);

                var toolStopwatch = Stopwatch.StartNew();
                using var toolActivity = AgentTelemetry.Source.StartActivity("agent.tool_call");
                toolActivity?.SetTag("agent.tool_name", call.Name);

                // Execute tool with non-cooperative timeout protection via Task.WhenAny
                object? toolResult;
                try
                {
                    using var toolPerCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    toolPerCallCts.CancelAfter(_options.PerCallTimeout);

                    var invokeTask = tool.InvokeAsync(call.Arguments?.ToDictionary() ?? [], toolPerCallCts.Token).AsTask();
                    var timeoutTask = Task.Delay(_options.PerCallTimeout, CancellationToken.None);

                    var completed = await Task.WhenAny(invokeTask, timeoutTask);
                    if (completed != invokeTask)
                    {
                        toolActivity?.SetTag("agent.is_error", true);
                        return FailRun(runActivity, runStopwatch,
                            $"TIMEOUT: Tool call '{call.Name}' exceeded per-call timeout limit.",
                            toolsUsed, turn, totalToolCalls);
                    }

                    toolResult = await invokeTask;
                    toolActivity?.SetTag("agent.duration_ms", toolStopwatch.ElapsedMilliseconds);
                    toolActivity?.SetTag("agent.is_error", false);
                }
                catch (OperationCanceledException)
                {
                    toolActivity?.SetTag("agent.is_error", true);
                    return HandleCancellation(runActivity, runStopwatch, cancellationToken, linkedCts, toolsUsed, turn, totalToolCalls);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool '{ToolName}' threw an unhandled exception.", call.Name);
                    toolActivity?.SetTag("agent.is_error", true);
                    return FailRun(runActivity, runStopwatch,
                        $"TOOL_ERROR: Tool '{call.Name}' encountered an execution error.",
                        toolsUsed, turn, totalToolCalls);
                }

                // Serialize with structured failure handling
                string serialized;
                try
                {
                    serialized = JsonSerializer.Serialize(toolResult, JsonSerializerOptions.Web);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to serialize output from tool '{ToolName}'.", call.Name);
                    return FailRun(runActivity, runStopwatch,
                        $"SERIALIZATION_ERROR: Output from tool '{call.Name}' could not be serialized.",
                        toolsUsed, turn, totalToolCalls);
                }

                var byteCount = Encoding.UTF8.GetByteCount(serialized);
                toolActivity?.SetTag("agent.payload_bytes", byteCount);

                if (byteCount > _options.MaxToolPayloadBytes)
                {
                    return FailRun(runActivity, runStopwatch,
                        $"PAYLOAD_LIMIT_EXCEEDED: Tool '{call.Name}' payload size ({byteCount} bytes) exceeded maximum of {_options.MaxToolPayloadBytes} bytes.",
                        toolsUsed, turn, totalToolCalls);
                }

                // Check pagination result contract: inspect HasMore property
                if (CheckHasMorePagination(toolResult, serialized, out var hasMore) && hasMore)
                {
                    var note = $"Results from {call.Name} were truncated by pagination limits.";
                    if (!limitations.Contains(note)) limitations.Add(note);
                }

                messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, serialized)]));
            }
        }

        return FailRun(runActivity, runStopwatch,
            $"TURN_LIMIT_EXCEEDED: Agent loop reached maximum of {_options.MaxModelTurns} turns without completing.",
            toolsUsed, _options.MaxModelTurns, totalToolCalls);
    }

    private static AgentRunResult FailRun(
        Activity? activity, Stopwatch stopwatch, string error, IReadOnlyList<string> toolsUsed, int turns, int toolCalls)
    {
        activity?.SetTag("agent.status", "failed");
        activity?.SetTag("agent.error_code", error.Split(':')[0].Trim());
        activity?.SetTag("agent.duration_ms", stopwatch.ElapsedMilliseconds);
        return AgentRunResult.Failed(error, toolsUsed, turns, toolCalls);
    }

    private static AgentRunResult HandleCancellation(
        Activity? activity, Stopwatch stopwatch, CancellationToken callerCt, CancellationTokenSource linkedCts,
        IReadOnlyList<string> toolsUsed, int turns, int toolCalls)
    {
        if (callerCt.IsCancellationRequested)
        {
            return FailRun(activity, stopwatch, "CANCELLED: Agent request was cancelled by caller.", toolsUsed, turns, toolCalls);
        }

        return FailRun(activity, stopwatch, "TIMEOUT: Overall request deadline of 60s exceeded.", toolsUsed, turns, toolCalls);
    }

    private static string CanonicalizeSignature(string name, IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0) return $"{name}()";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalValue(writer, arguments);
        }
        return $"{name}({Encoding.UTF8.GetString(stream.ToArray())})";
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case IDictionary<string, object?> dict:
                writer.WriteStartObject();
                foreach (var kvp in dict.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(kvp.Key);
                    WriteCanonicalValue(writer, kvp.Value);
                }
                writer.WriteEndObject();
                break;
            case IEnumerable<object?> list when value is not string:
                writer.WriteStartArray();
                foreach (var item in list) WriteCanonicalValue(writer, item);
                writer.WriteEndArray();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    private static int ExtractPageNumber(IDictionary<string, object?>? arguments)
    {
        if (arguments is null) return 1;
        if (arguments.TryGetValue("page", out var raw) && raw is not null)
        {
            if (raw is int i) return i;
            if (int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return 1;
    }

    private static bool CheckHasMorePagination(object? result, string serializedJson, out bool hasMore)
    {
        hasMore = false;
        if (result is null) return false;

        // 1. Direct typed property check
        var prop = result.GetType().GetProperty("HasMore");
        if (prop?.GetValue(result) is bool b)
        {
            hasMore = b;
            return true;
        }

        // 2. Structured JSON element check
        try
        {
            using var doc = JsonDocument.Parse(serializedJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("hasMore", out var elem) &&
                elem.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                hasMore = elem.GetBoolean();
                return true;
            }
        }
        catch { }

        return false;
    }
}
```

---

## 6. Workstream 4 — Observability & OpenTelemetry Tracing

### 6.1 Telemetry Definition: `AgentTelemetry.cs`
Activity source registration following OpenTelemetry semantic conventions:

```csharp
namespace PublishingTracker.Agent.Telemetry;

public static class AgentTelemetry
{
    public const string ActivitySourceName = "PublishingTracker.Agent";
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");
}
```

### 6.2 Data Privacy Invariants (Strict Redaction)
In accordance with `ADR-008` (Model Data Privacy and Prompt Boundary):
1. **Never tag sensitive strings:** Spans must **never** record raw user questions, system prompts, model text completions, tool input arguments, full serialized tool result JSON, or bearer tokens in Activity Tags or Events.
2. **Recorded tags are strictly structural and numerical:**
   - Root `agent.run`: `agent.max_turns`, `agent.max_tool_calls`, `agent.total_tool_calls`, `agent.status` (`completed` / `failed`), `agent.duration_ms`, `agent.error_code` (on failure).
   - `agent.model_turn`: `agent.turn_number`, `agent.duration_ms`, `agent.has_function_calls`.
   - `agent.tool_call`: `agent.tool_name`, `agent.payload_bytes`, `agent.duration_ms`, `agent.is_error`.

---

## 7. Workstream 5 — REST API Endpoint (`POST /api/agent/query`)

Map an authenticated Minimal API route in `PublishingTracker.Agent`:

```csharp
app.MapPost("/api/agent/query", async (
    [FromBody] AgentQueryRequest request,
    [FromServices] AgentLoopRunner runner,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question must not be empty." });

    var outcome = await runner.RunAsync(request.Question, ct);

    return outcome.IsSuccess
        ? Results.Ok(new AgentQueryResponse(
            Status: "answered",
            Answer: outcome.Answer,
            ToolsUsed: outcome.ToolsUsed,
            Turns: outcome.Turns,
            TotalToolCalls: outcome.TotalToolCalls,
            Limitations: outcome.Limitations,
            Error: null))
        : Results.Ok(new AgentQueryResponse(
            Status: "could_not_answer",
            Answer: null,
            ToolsUsed: outcome.ToolsUsed,
            Turns: outcome.Turns,
            TotalToolCalls: outcome.TotalToolCalls,
            Limitations: outcome.Limitations,
            Error: outcome.ErrorMessage));
}).RequireAuthorization();
```

---

## 8. Workstream 6 — Deterministic Test Suite (`PublishingTracker.Agent.Tests`)

Create a dedicated test project `tests/PublishingTracker.Agent.Tests/PublishingTracker.Agent.Tests.csproj` implementing:

### 8.1 Test Double: `ScriptedChatClient.cs`
A fully deterministic test implementation of `IChatClient` that replays pre-configured turns without external dependencies:

```csharp
namespace PublishingTracker.Agent.Tests.Doubles;

public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<Func<IList<ChatMessage>, ChatResponse>> _responses = new();
    public List<IList<ChatMessage>> Invocations { } = [];

    public void EnqueueFunctionCall(string toolName, IDictionary<string, object?> arguments)
    {
        _responses.Enqueue(_ => new ChatResponse(new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent(Guid.NewGuid().ToString("N"), toolName, arguments)])));
    }

    public void EnqueueTextResponse(string text)
    {
        _responses.Enqueue(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public Task<ChatResponse> GetResponseAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add([.. chatMessages]);
        if (_responses.TryDequeue(out var responder))
            return Task.FromResult(responder(chatMessages));

        throw new InvalidOperationException("No scripted response remaining in ScriptedChatClient.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

### 8.2 Required Phase 2 Test Scenarios (18 Deterministic Tests)

#### Category A: Agent Loop Mechanics & Happy Path
1. **`AgentLoop_TwoToolSequence_CompletesWithGroundedAnswer`**
   - Script turn 1: Call `find_titles_missing_metadata(format: "Audiobook")`.
   - Script turn 2: Call `get_title_details(bookId: 42)`.
   - Script turn 3: Emit final completion text `"Found 1 title."`.
   - Assert `outcome.IsSuccess == true`, `outcome.Turns == 3`, `outcome.TotalToolCalls == 2`, and `toolsUsed` equals `["find_titles_missing_metadata", "get_title_details"]`.
2. **`AgentLoop_UnfinishedPagination_EmitsMandatoryLimitations`**
   - Stub tool returns `MissingMetadataPageDto` with `HasMore: true`.
   - Complete execution.
   - Assert `outcome.Limitations` is non-empty and cites pagination truncation.
3. **`AgentLoop_ModelEmitsEmptyText_HaltsWithEmptyResponseFailure`**
   - Script model returning turn with no function calls and empty or whitespace text.
   - Assert loop fails with structured `"EMPTY_RESPONSE"` failure.

#### Category B: Budget Guards & Canonicalization
4. **`AgentLoop_MaxModelTurnsExceeded_HaltsWithStructuredFailure`**
   - Script client to return function calls for 7 consecutive turns.
   - Assert loop halts at turn 6 with `"TURN_LIMIT_EXCEEDED"`.
5. **`AgentLoop_MaxTotalToolCallsExceeded_AcrossMultipleTurns_HaltsWithBudgetError`**
   - Script turns returning 3 calls, then 3 calls, then 3 calls (total 9 calls).
   - Assert loop halts on the 9th call with `"BUDGET_EXCEEDED"`.
6. **`AgentLoop_MaxTotalToolCallsExceeded_WithinSingleTurn_HaltsImmediately`**
   - Script turn 1 returning 9 distinct tool calls in a single response message.
   - Assert loop halts on the 9th call without invoking beyond the 8-call budget.
7. **`AgentLoop_RepeatCallLimitExceeded_WithPermutedArgumentKeys_HaltsWithRepeatCallError`**
   - Turn 1: call `find_titles_missing_metadata(page: 1, format: "Ebook")`.
   - Turn 2: call `find_titles_missing_metadata(format: "Ebook", page: 1)` (permuted key order).
   - Turn 3: call `find_titles_missing_metadata(page: 1, format: "Ebook")`.
   - Assert canonicalized recursive signature matching trips `RepeatCallLimit = 2` on the 3rd invocation with `"REPEAT_CALL_LIMIT"`.
8. **`AgentLoop_PayloadSizeCap_RejectsExcessivePayload`**
   - Stub tool returns a payload exceeding 16,384 bytes.
   - Assert loop halts with `"PAYLOAD_LIMIT_EXCEEDED"` without appending to history.
9. **`AgentLoop_PaginationLimitExceeded_RejectsPageBeyondCap`**
   - Model calls `find_titles_missing_metadata(page: 3)`.
   - Assert loop halts with `"PAGINATION_LIMIT_EXCEEDED"` and does not invoke tool.

#### Category C: Fault Tolerance, Hallucinations & Timeouts
10. **`AgentLoop_UnknownToolRequested_HaltsGracefullyWithUnknownToolError`**
    - Script model requesting hallucinated tool `delete_all_records()`.
    - Assert runner intercepts, records no execution, and returns structured `"UNKNOWN_TOOL"` failure.
11. **`AgentLoop_DuplicateMcpToolNames_HaltsWithInvalidToolsetError`**
    - Tool provider returns duplicate tool named `find_titles_missing_metadata`.
    - Assert runner intercepts without unhandled exception and returns `"INVALID_TOOLSET"`.
12. **`AgentLoop_ToolThrowsException_HaltsGracefullyWithMaskedError`**
    - Stub tool throws `HttpRequestException("Sensitive database server address")`.
    - Assert runner catches exception, logs stack internally, and returns generic `"TOOL_ERROR"` masking internal server details.
13. **`AgentLoop_ToolSerializationFails_HaltsWithSerializationError`**
    - Stub tool returns un-serializable cyclic object.
    - Assert runner catches `JsonException` and returns `"SERIALIZATION_ERROR"`.
14. **`AgentLoop_ModelCallTimeout_CancelsAndFailsGracefully`**
    - Inject model turn delayed beyond `PerCallTimeout`.
    - Assert graceful `"TIMEOUT: Model call exceeded per-call timeout limit."`.
15. **`AgentLoop_ToolCallTimeout_NonCooperativeTool_AbortsExecution`**
    - Inject non-cooperative tool that ignores `CancellationToken` and delays indefinitely.
    - Assert `Task.WhenAny` triggers timeout and aborts runner.
16. **`AgentLoop_OverallDeadlineExceeded_HaltsExecution`**
    - Run execution where cumulative delays exceed `OverallTimeout`.
    - Assert linked CTS cancels and returns `"TIMEOUT: Overall request deadline of 60s exceeded."`.

#### Category D: Security, Privacy & Telemetry
17. **`OpenTelemetry_EmitsSpansWithoutSensitiveData`**
    - Attach an `ActivityListener` for `PublishingTracker.Agent`.
    - Run a multi-step agent execution.
    - Assert root span `agent.run` and child spans `agent.model_turn` and `agent.tool_call` are recorded with duration and status.
    - Assert all tag values: verify **zero** occurrences of user prompt text, tool argument values, payload bodies, or tokens.
18. **`PerRequestBearerHandler_AttachesToken_ReplacesExistingHeader_AndRejectsInsecureEndpoints`**
    - Test handler with mock `IHttpContextAccessor`.
    - Pre-populate request with malicious `Authorization: Bearer fake`.
    - Verify handler removes pre-existing header and attaches validated caller token.
    - Verify target endpoints using plain HTTP to external domains throw `InvalidOperationException`.

---

## 9. Execution Sequence & Acceptance Checklist

### Step-by-Step Implementation Sequence
1. **Step 2.1 — Package & Project Setup:**
   - Update `Directory.Packages.props` with `Microsoft.Extensions.AI` (`10.10.0`), `Microsoft.Extensions.AI.OpenAI` (`10.10.0`), and `OpenTelemetry.Extensions.Hosting` (`1.16.0`).
   - Create `src/PublishingTracker.Agent/PublishingTracker.Agent.csproj` and add to `PublishingTracker.Api.sln`.
   - Create `tests/PublishingTracker.Agent.Tests/PublishingTracker.Agent.Tests.csproj` and add to solution.
2. **Step 2.2 — Configuration & Setup:**
   - Implement `AIProviderOptions.cs` and `AIProviderSetup.cs` with explicit provider validation, OpenRouter privacy headers, and custom `HttpClient` wiring.
   - Write `RecordingHttpMessageHandler` tests verifying provider resolution, header policies, and secrets redaction.
3. **Step 2.3 — Outbound Auth & MCP Tool Provider:**
   - Implement `PerRequestBearerHandler.cs` with destination URL validation and header replacement.
   - Implement `StreamableHttpMcpToolProvider.cs`.
4. **Step 2.4 — Loop Mechanics & Telemetry:**
   - Implement `AgentLoopOptions.cs` with `Validate()`, `AgentLoopRunner.cs` with recursive canonicalization and timeout protections, and `AgentTelemetry.cs`.
5. **Step 2.5 — API Endpoint & Minimal API:**
   - Add `POST /api/agent/query` route in `PublishingTracker.Agent/Program.cs`.
6. **Step 2.6 — Deterministic Unit & Telemetry Test Suite:**
   - Implement `ScriptedChatClient.cs` and all 18 test scenarios in `AgentLoopTests.cs`.
7. **Step 2.7 — Build & Verification:**
   - Execute `dotnet build` and `dotnet test` across the entire solution.

### Phase 2 Acceptance Checklist
- [ ] `PublishingTracker.Agent` compiles cleanly on `net10.0` with `Microsoft.Extensions.AI`
- [ ] `AIProviderSetup` rejects invalid provider types at startup and wires `customHttpClient` to `OpenAIClientOptions.Transport`
- [ ] `PerRequestBearerHandler` validates destination HTTPS/localhost, replaces pre-existing headers, and forwards caller JWT per outbound request
- [ ] Dual budgets enforce `MaxModelTurns = 6` and `MaxTotalToolCalls = 8` (counting individual calls within single turns)
- [ ] Recursive JSON canonicalization prevents duplicate tool calls regardless of argument key ordering
- [ ] Tool payload byte limit (16 KB) and pagination boundaries (max 2 pages) are strictly enforced
- [ ] Timeouts differentiate caller cancellation, overall deadline, model timeout, and tool timeout (with non-cooperative tool protection)
- [ ] Tool errors, duplicate tool names, serialization failures, and empty model responses return structured public failure codes
- [ ] OpenTelemetry root and child spans are emitted with execution metrics and strictly zero sensitive data tags
- [ ] All 18 deterministic test scenarios pass in CI with zero spend
