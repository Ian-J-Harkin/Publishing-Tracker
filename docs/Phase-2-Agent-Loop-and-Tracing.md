# Phase 2 — Expanded Design: Agent Loop, Dual Budgets, and Basic Tracing

Companion phase specification to [MCP-Agent-Project-Plan.md](../MCP-Agent-Project-Plan.md), [MCP-Agent-Design-Log.md](MCP-Agent-Design-Log.md), [Phase-0-Baseline-and-Contracts.md](Phase-0-Baseline-and-Contracts.md), and [Phase-1-Read-Only-Catalogue-and-MCP.md](Phase-1-Read-Only-Catalogue-and-MCP.md).

---

## 1. Purpose, Scope, and System Boundaries

### Goal
Implement `PublishingTracker.Agent` as an authenticated ASP.NET Core service hosting an **explicit, dual-budget orchestration loop** powered by `Microsoft.Extensions.AI` (`IChatClient`). Connect to `PublishingTracker.Api`'s `/mcp` endpoint over Streamable HTTP using per-request JWT forwarding (never shared `DefaultRequestHeaders`), enforce deterministic execution controls (`MaxModelTurns = 6`, `MaxTotalToolCalls = 8`, payload caps, normalized repeat-call limits, 2-page pagination boundaries), emit privacy-safe OpenTelemetry activity traces, and verify behavior with deterministic test doubles (`ScriptedChatClient` and `RecordingHttpMessageHandler`).

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
2. **Configuration & Provider Setup (`AIProviderOptions` / `AIProviderSetup`)**: Ported and hardened from `ai-code-review-bot`, supporting OpenRouter (primary, pinned Claude model) + Ollama, OpenAI, and Azure OpenAI fallbacks, with `ValidateOnStart()`, `[JsonIgnore]` on secrets, redacted `ToString()`, and OpenRouter privacy headers (`HTTP-Referer`, `X-Title`, and `data_collection: "deny"`).
3. **Outbound MCP Client Transport with Per-Request Bearer Propagation**: `PerRequestBearerHandler` delegating handler that extracts the incoming caller JWT from `IHttpContextAccessor` and attaches it per `HttpRequestMessage` to `/mcp`, ensuring multi-tenant isolation across the HTTP boundary.
4. **Explicit Dual-Budget Agent Loop (`AgentLoopRunner` & `AgentLoopOptions`)**:
   - `MaxModelTurns = 6`
   - `MaxTotalToolCalls = 8` (enforced across the entire run, counting every function call returned by model turns)
   - `MaxToolPayloadBytes = 16_384` (16 KB tool response truncation / guard)
   - `RepeatCallLimit = 2` (normalized call signature: tool name + canonicalized JSON arguments)
   - `PerCallTimeout = 20s`, `OverallTimeout = 60s`
   - 2-page pagination cap with mandatory `limitations` note when `hasMore` remains `true`
5. **Observability & OpenTelemetry (`ActivitySource("PublishingTracker.Agent")`)**: Root span `agent.run` and child spans `agent.model_turn` and `agent.tool_call` with execution metadata (turn numbers, tool names, durations, payload sizes, status codes), strictly excluding sensitive data (no prompts, tool arguments, full tool outputs, or bearer tokens).
6. **Agent Query Endpoint**: `POST /api/agent/query` accepting `AgentQueryRequest` and returning `AgentQueryResponse`.
7. **Test Suite (`PublishingTracker.Agent.Tests`)**: Deterministic unit tests with `ScriptedChatClient` (simulating tool turns and completions without external API calls) and `RecordingHttpMessageHandler` (validating provider configuration, headers, and endpoints without spend).

### Out of Scope for Phase 2
- **Evidence Ledger & Stage 2 Structured Output Verification** (Phase 3: `EvidenceLedger`, `AgentOutputValidator`, citation provenance, and JSON schema repair turns).
- **RAG & `pgvector` Document Retrieval** (Phase 4: `PublishingTracker.Rag` and `search_publishing_guidelines`).
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

    public string Type { get; set; } = "openrouter"; // openrouter, openai, azureopenai, ollama
    public string ModelId { get; set; } = "anthropic/claude-sonnet-4"; // Pinned Claude on OpenRouter
    public string? Endpoint { get; set; } = "https://openrouter.ai/api/v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    public string OpenRouterModelId { get; set; } = "anthropic/claude-sonnet-4";
    public string? OpenRouterEndpoint { get; set; } = "https://openrouter.ai/api/v1";

    [System.Text.Json.Serialization.JsonIgnore]
    public string OpenRouterApiKey { get; set; } = string.Empty;

    public string OllamaModelId { get; set; } = "qwen2.5-coder:7b";
    public string? OllamaEndpoint { get; set; } = "http://localhost:11434/v1";

    public string McpEndpoint { get; set; } = "https://localhost:7001/mcp";

    public override string ToString() =>
        $"AIProviderOptions {{ Type = {Type}, ModelId = {ModelId}, Endpoint = {Endpoint}, ApiKey = {Redact(ApiKey)}, McpEndpoint = {McpEndpoint} }}";

    private static string Redact(string? val) => string.IsNullOrWhiteSpace(val) ? "<empty>" : "[REDACTED]";
}
```

### 3.3 Provider Resolution & Client Factory: `AIProviderSetup.cs`
Resolves options with environment variable fallbacks (`OPENROUTER_API_KEY`, `OPENAI_API_KEY`, `OPENROUTER_MODEL_NAME`, `OPENAI_MODEL_NAME`), configures outbound headers for OpenRouter privacy compliance (`data_collection: "deny"`, `X-Title: PublishingTracker`), and constructs an `IChatClient`:

```csharp
namespace PublishingTracker.Agent.Configuration;

public static class AIProviderSetup
{
    public const string OpenRouterDefaultEndpoint = "https://openrouter.ai/api/v1";
    public const string OpenRouterDefaultModelId = "anthropic/claude-sonnet-4";

    public static AIProviderOptions Resolve(AIProviderOptions configured, Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(getEnv);

        var type = configured.Type.ToLowerInvariant();
        if (type == "openrouter")
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

        if (type == "ollama")
        {
            return new AIProviderOptions
            {
                Type = "ollama",
                ModelId = FirstNonEmpty(configured.OllamaModelId, configured.ModelId, "qwen2.5-coder:7b")!,
                Endpoint = FirstNonEmpty(configured.OllamaEndpoint, configured.Endpoint, "http://localhost:11434/v1"),
                ApiKey = FirstNonEmpty(configured.ApiKey, "ollama")!,
                McpEndpoint = configured.McpEndpoint
            };
        }

        // openai / azureopenai fallbacks
        var openAiKey = FirstNonEmpty(getEnv("OPENAI_API_KEY"), configured.ApiKey) ?? string.Empty;
        var openAiModel = FirstNonEmpty(getEnv("OPENAI_MODEL_NAME"), configured.ModelId, "gpt-4o-mini")!;
        return new AIProviderOptions
        {
            Type = configured.Type,
            ModelId = openAiModel,
            Endpoint = configured.Endpoint,
            ApiKey = openAiKey,
            McpEndpoint = configured.McpEndpoint
        };
    }

    public static IChatClient CreateChatClient(
        AIProviderOptions options,
        HttpClient? customHttpClient = null)
    {
        if (options.Type != "ollama" && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException($"API key is required for provider '{options.Type}'. Set OPENROUTER_API_KEY or AIProvider:ApiKey.");
        }

        var openAiOptions = new OpenAI.OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            openAiOptions.Endpoint = new Uri(options.Endpoint);
        }

        if (options.Type == "openrouter")
        {
            // Privacy & attribution headers required by ADR-008
            openAiOptions.AddPolicy(new OpenRouterHeadersPolicy(), ClientPipelinePosition.PerCall);
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

### 4.1 Per-Request JWT Propagation: `PerRequestBearerHandler.cs`
Enforces `ADR-005`: never set Bearer tokens on shared `HttpClient.DefaultRequestHeaders`. The handler extracts the caller's validated token from `IHttpContextAccessor` and adds it directly to the outgoing `HttpRequestMessage.Headers`:

```csharp
namespace PublishingTracker.Agent.Auth;

public sealed class PerRequestBearerHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HttpContext for outbound MCP request.");

        if (context.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Caller must be authenticated.");

        var rawHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(rawHeader) || !rawHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Missing or invalid Bearer token.");

        // Attach per request message only (safe for concurrent requests with different callers)
        request.Headers.TryAddWithoutValidation("Authorization", rawHeader);

        return base.SendAsync(request, cancellationToken);
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

### 5.1 Configuration: `AgentLoopOptions.cs`
Locked defaults based on `ADR-006` and project specifications:

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
}
```

### 5.2 Loop Runner: `AgentLoopRunner.cs`
Orchestrates turns against `IChatClient`, executing tool calls step by step without automatic wrapper middleware, ensuring budgets are strictly counted:

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
        _logger = logger;
    }

    public async Task<AgentRunResult> RunAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        using var runActivity = AgentTelemetry.Source.StartActivity("agent.run");
        runActivity?.SetTag("agent.max_turns", _options.MaxModelTurns);
        runActivity?.SetTag("agent.max_tool_calls", _options.MaxTotalToolCalls);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.OverallTimeout);
        var ct = linkedCts.Token;

        var tools = await _toolProvider.GetToolsAsync(ct);
        var toolMap = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var chatOptions = new ChatOptions { Tools = [.. tools] };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AgentPrompts.SystemPromptV1),
            new(ChatRole.User, userPrompt)
        };

        var toolsUsed = new List<string>();
        var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalToolCalls = 0;
        var limitations = new List<string>();

        for (var turn = 1; turn <= _options.MaxModelTurns; turn++)
        {
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
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AgentRunResult.Failed("TIMEOUT: Model call exceeded per-call timeout limit.", toolsUsed, turn, totalToolCalls);
            }

            var functionCalls = responseMessage.Contents.OfType<FunctionCallContent>().ToList();
            if (functionCalls.Count == 0)
            {
                // Model finished tool interaction; candidate final answer reached
                return AgentRunResult.Completed(responseMessage.Text ?? string.Empty, toolsUsed, turn, totalToolCalls, limitations);
            }

            // Process every tool call returned in this turn
            foreach (var call in functionCalls)
            {
                totalToolCalls++;
                runActivity?.SetTag("agent.total_tool_calls", totalToolCalls);

                if (totalToolCalls > _options.MaxTotalToolCalls)
                {
                    return AgentRunResult.Failed(
                        $"BUDGET_EXCEEDED: Maximum tool-call budget of {_options.MaxTotalToolCalls} exceeded.",
                        toolsUsed, turn, totalToolCalls);
                }

                if (!toolMap.TryGetValue(call.Name, out var tool))
                {
                    return AgentRunResult.Failed(
                        $"UNKNOWN_TOOL: Requested tool '{call.Name}' does not exist.",
                        toolsUsed, turn, totalToolCalls);
                }

                // Canonicalize signature for repeat detection: Name(arg1=val,arg2=val)
                var callSignature = NormalizeSignature(call.Name, call.Arguments);
                callCounts[callSignature] = callCounts.GetValueOrDefault(callSignature, 0) + 1;
                if (callCounts[callSignature] > _options.RepeatCallLimit)
                {
                    return AgentRunResult.Failed(
                        $"REPEAT_CALL_LIMIT: Tool call '{call.Name}' with identical arguments exceeded repeat limit of {_options.RepeatCallLimit}.",
                        toolsUsed, turn, totalToolCalls);
                }

                if (!toolsUsed.Contains(call.Name))
                    toolsUsed.Add(call.Name);

                using var toolActivity = AgentTelemetry.Source.StartActivity("agent.tool_call");
                toolActivity?.SetTag("agent.tool_name", call.Name);

                object? toolResult;
                try
                {
                    using var toolPerCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    toolPerCallCts.CancelAfter(_options.PerCallTimeout);

                    toolResult = await tool.InvokeAsync(call.Arguments?.ToDictionary() ?? [], toolPerCallCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return AgentRunResult.Failed($"TIMEOUT: Tool call '{call.Name}' exceeded per-call timeout.", toolsUsed, turn, totalToolCalls);
                }
                catch (Exception ex)
                {
                    return AgentRunResult.Failed($"TOOL_ERROR: Tool '{call.Name}' failed: {ex.Message}", toolsUsed, turn, totalToolCalls);
                }

                var serialized = JsonSerializer.Serialize(toolResult, JsonSerializerOptions.Web);
                var byteCount = Encoding.UTF8.GetByteCount(serialized);
                toolActivity?.SetTag("agent.payload_bytes", byteCount);

                if (byteCount > _options.MaxToolPayloadBytes)
                {
                    return AgentRunResult.Failed(
                        $"PAYLOAD_LIMIT_EXCEEDED: Tool '{call.Name}' payload size ({byteCount} bytes) exceeded maximum of {_options.MaxToolPayloadBytes} bytes.",
                        toolsUsed, turn, totalToolCalls);
                }

                // Check pagination bounds: inspect if hasMore is true
                if (HasUnfinishedPagination(toolResult, out var hasMore) && hasMore)
                {
                    limitations.Add($"Results from {call.Name} were truncated by pagination limits.");
                }

                messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, serialized)]));
            }
        }

        return AgentRunResult.Failed(
            $"TURN_LIMIT_EXCEEDED: Agent loop reached maximum of {_options.MaxModelTurns} turns without completing.",
            toolsUsed, _options.MaxModelTurns, totalToolCalls);
    }

    private static string NormalizeSignature(string name, IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0) return $"{name}()";
        var sorted = string.Join(",", arguments.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        return $"{name}({sorted})";
    }

    private static bool HasUnfinishedPagination(object? result, out bool hasMore)
    {
        hasMore = false;
        if (result is null) return false;
        // Check for MissingMetadataPageDto.HasMore or similar properties
        var prop = result.GetType().GetProperty("HasMore");
        if (prop?.GetValue(result) is bool b)
        {
            hasMore = b;
            return true;
        }
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
   - Root `agent.run`: `agent.max_turns`, `agent.max_tool_calls`, `agent.total_tool_calls`, `agent.status` (`completed` / `failed`).
   - `agent.model_turn`: `agent.turn_number`, `agent.model_id`.
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

### 8.2 Required Phase 2 Test Scenarios (16 Deterministic Tests)

#### Category A: Agent Loop Mechanics & Happy Path
1. **`AgentLoop_TwoToolSequence_CompletesWithGroundedAnswer`**
   - Script turn 1: Call `find_titles_missing_metadata(format: "Audiobook")`.
   - Script turn 2: Call `get_title_details(bookId: 42)`.
   - Script turn 3: Emit final completion text.
   - Assert `outcome.IsSuccess == true`, `outcome.Turns == 3`, `outcome.TotalToolCalls == 2`, and `toolsUsed` equals `["find_titles_missing_metadata", "get_title_details"]`.
2. **`AgentLoop_UnfinishedPagination_EmitsMandatoryLimitations`**
   - Stub tool returns `MissingMetadataPageDto` with `HasMore: true`.
   - Complete execution.
   - Assert `outcome.Limitations` is non-empty and cites pagination truncation.

#### Category B: Budget Guards & Canonicalization
3. **`AgentLoop_MaxModelTurnsExceeded_HaltsWithStructuredFailure`**
   - Script client to return function calls for 7 consecutive turns.
   - Assert loop halts at turn 6 with `"TURN_LIMIT_EXCEEDED"`.
4. **`AgentLoop_MaxTotalToolCallsExceeded_AcrossMultipleTurns_HaltsWithBudgetError`**
   - Script turns returning 3 calls, then 3 calls, then 3 calls (total 9 calls).
   - Assert loop halts on the 9th call with `"BUDGET_EXCEEDED"`.
5. **`AgentLoop_MaxTotalToolCallsExceeded_WithinSingleTurn_HaltsImmediately`**
   - Script turn 1 returning 9 distinct tool calls in a single response message.
   - Assert loop halts on the 9th call without invoking beyond the 8-call budget.
6. **`AgentLoop_RepeatCallLimitExceeded_WithPermutedArgumentKeys_HaltsWithRepeatCallError`**
   - Turn 1: call `find_titles_missing_metadata(page: 1, format: "Ebook")`.
   - Turn 2: call `find_titles_missing_metadata(format: "Ebook", page: 1)` (permuted key order).
   - Turn 3: call `find_titles_missing_metadata(page: 1, format: "Ebook")`.
   - Assert canonicalized signature matching trips `RepeatCallLimit = 2` on the 3rd invocation with `"REPEAT_CALL_LIMIT"`.
7. **`AgentLoop_PayloadSizeCap_RejectsExcessivePayload`**
   - Stub tool returns a payload exceeding 16,384 bytes.
   - Assert loop halts with `"PAYLOAD_LIMIT_EXCEEDED"` without running subsequent turns.

#### Category C: Fault Tolerance, Hallucinations & Timeouts
8. **`AgentLoop_UnknownToolRequested_HaltsGracefullyWithUnknownToolError`**
   - Script model requesting hallucinated tool `delete_all_records()`.
   - Assert runner intercepts, records no execution, and returns structured `"UNKNOWN_TOOL"` failure.
9. **`AgentLoop_ToolThrowsException_HaltsGracefullyWithStructuredError`**
   - Stub tool throws `HttpRequestException("503 Service Unavailable")`.
   - Assert runner catches exception, preserves `toolsUsed`, and returns `"TOOL_ERROR"`.
10. **`AgentLoop_PerCallTimeout_CancelsAndFailsGracefully`**
    - Inject a tool delayed beyond `PerCallTimeout` (e.g. 50ms in a fast test configuration).
    - Assert graceful `"TIMEOUT: Model call exceeded per-call timeout limit."` or tool timeout.
11. **`AgentLoop_OverallDeadlineExceeded_HaltsExecution`**
    - Run execution where cumulative turn delays exceed `OverallTimeout`.
    - Assert linked CTS cancels and returns overall timeout failure.

#### Category D: Security, Privacy & Telemetry
12. **`OpenTelemetry_EmitsSpansWithoutSensitiveData`**
    - Attach an `ActivityListener` for `PublishingTracker.Agent`.
    - Run a multi-step agent execution.
    - Assert root span `agent.run` and child spans `agent.model_turn` and `agent.tool_call` are recorded.
    - Assert all tag values: verify **zero** occurrences of user prompt text, tool argument values, payload bodies, or tokens.
13. **`PerRequestBearerHandler_AttachesTokenPerOutboundRequest_AndRejectsMissingContext`**
    - Test handler attached to `HttpClient` with mock `IHttpContextAccessor`.
    - Verify `Authorization: Bearer <token>` is attached to `HttpRequestMessage.Headers`.
    - Verify unauthenticated or missing token contexts throw `UnauthorizedAccessException`.
14. **`PerRequestBearerHandler_ConcurrentRequests_DoNotBleedTokensAcrossTenants`**
    - Simulate concurrent requests from User A and User B.
    - Assert outbound requests receive only the respective caller's token.

#### Category E: Configuration, Provider Setup & Secrets Redaction
15. **`AIProviderSetup_RoutingAndHeaderTests`**
    - Using `RecordingHttpMessageHandler`, verify `openrouter`, `ollama`, `openai`, and `azureopenai` configurations resolve properly.
    - Assert `data_collection: "deny"` and `X-Title` headers are attached for OpenRouter.
16. **`AIProviderOptions_ToString_RedactsApiKeys`**
    - Populate `AIProviderOptions` with sensitive keys (`ApiKey`, `OpenRouterApiKey`).
    - Assert `ToString()` outputs `[REDACTED]` and never leaks plain-text keys in logs.

---

## 9. Execution Sequence & Acceptance Checklist

### Step-by-Step Implementation Sequence
1. **Step 2.1 — Package & Project Setup:**
   - Update `Directory.Packages.props` with `Microsoft.Extensions.AI` (`10.10.0`), `Microsoft.Extensions.AI.OpenAI` (`10.10.0`), and `OpenTelemetry.Extensions.Hosting` (`1.16.0`).
   - Create `src/PublishingTracker.Agent/PublishingTracker.Agent.csproj` and add to `PublishingTracker.Api.sln`.
   - Create `tests/PublishingTracker.Agent.Tests/PublishingTracker.Agent.Tests.csproj` and add to solution.
2. **Step 2.2 — Configuration & Setup:**
   - Implement `AIProviderOptions.cs` and `AIProviderSetup.cs` with OpenRouter privacy headers and fallbacks.
   - Write `RecordingHttpMessageHandler` tests verifying provider resolution and headers.
3. **Step 2.3 — Outbound Auth & MCP Tool Provider:**
   - Implement `PerRequestBearerHandler.cs` and `StreamableHttpMcpToolProvider.cs`.
4. **Step 2.4 — Loop Mechanics & Telemetry:**
   - Implement `AgentLoopOptions.cs`, `AgentLoopRunner.cs`, and `AgentTelemetry.cs`.
5. **Step 2.5 — API Endpoint & Minimal API:**
   - Add `POST /api/agent/query` route in `PublishingTracker.Agent/Program.cs`.
6. **Step 2.6 — Deterministic Unit & Telemetry Test Suite:**
   - Implement `ScriptedChatClient.cs` and the 10 test scenarios in `AgentLoopTests.cs`.
7. **Step 2.7 — Build & Verification:**
   - Execute `dotnet build` and `dotnet test` across the entire solution.

### Phase 2 Acceptance Checklist
- [ ] `PublishingTracker.Agent` compiles cleanly on `net10.0` with `Microsoft.Extensions.AI`
- [ ] `AIProviderSetup` resolves OpenRouter with pinned Claude model and attaches `data_collection: "deny"` header
- [ ] `PerRequestBearerHandler` forwards incoming caller JWT to `/mcp` per outbound request without shared header mutation
- [ ] Dual budgets enforce `MaxModelTurns = 6` and `MaxTotalToolCalls = 8` (counting individual calls within single turns)
- [ ] Repeat call guard halts execution when identical tool signature is called more than 2 times
- [ ] Tool payload byte limit (16 KB) prevents memory bloat from oversized responses
- [ ] OpenTelemetry root and child spans are emitted with execution metrics and strictly zero sensitive data tags
- [ ] All 16 deterministic test scenarios pass in CI with zero spend
