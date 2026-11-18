using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PublishingTracker.Agent.Auth;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Orchestration;
using PublishingTracker.Agent.Telemetry;
using PublishingTracker.Agent.Tests.Doubles;

namespace PublishingTracker.Agent.Tests;

public sealed class AgentLoopTests
{
    [Fact]
    public async Task AgentLoop_TwoToolSequence_CompletesWithGroundedAnswer()
    {
        var tools = new StubToolProvider(
            Tool("find_titles_missing_metadata", (_, _, _, _) => Task.FromResult<object?>(new { page = 1, hasMore = false, items = new[] { 42 } })),
            Tool("get_title_details", (_, _, bookId, _) => Task.FromResult<object?>(new { bookId, title = "Example" })));
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook")));
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 42)));
        chat.EnqueueText("Found 1 title.");
        var result = await Runner(chat, tools).RunAsync("Find an audiobook.");
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Turns);
        Assert.Equal(2, result.TotalToolCalls);
        Assert.Equal(new[] { "find_titles_missing_metadata", "get_title_details" }, result.ToolsUsed);
        Assert.Equal(1, tools.OpenCount);
    }

    [Fact]
    public async Task AgentLoop_UnfinishedPagination_EmitsMandatoryLimitations()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 2)));
        chat.EnqueueText("There are more results.");
        var tools = new StubToolProvider(Tool("find_titles_missing_metadata", (_, _, _, _) => Task.FromResult<object?>(new { page = 2, hasMore = true })));
        var result = await Runner(chat, tools).RunAsync("Find missing metadata.");
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Limitations, note => note.Contains("incomplete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AgentLoop_ModelEmitsEmptyText_HaltsWithEmptyResponseFailure()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueText("  ");
        var result = await Runner(chat, new StubToolProvider()).RunAsync("Question");
        Assert.False(result.IsSuccess);
        Assert.StartsWith("EMPTY_RESPONSE:", result.ErrorMessage);
    }

    [Fact]
    public async Task AgentLoop_MaxModelTurnsExceeded_HaltsWithStructuredFailure()
    {
        var chat = new ScriptedChatClient();
        for (var i = 1; i <= 6; i++) chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", i)));
        var count = 0;
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => { count++; return Task.FromResult<object?>(new { ok = true }); }));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.False(result.IsSuccess);
        Assert.StartsWith("TURN_LIMIT_EXCEEDED:", result.ErrorMessage);
        Assert.Equal(6, result.Turns);
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task AgentLoop_MaxTotalToolCallsExceeded_AcrossMultipleTurns_HaltsWithBudgetError()
    {
        var chat = new ScriptedChatClient();
        for (var turn = 0; turn < 3; turn++)
            chat.EnqueueFunctionCalls(Enumerable.Range(1, 3).Select(i => Call("get_title_details", ("bookId", turn * 3 + i))).ToArray());
        var invoked = 0;
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => { invoked++; return Task.FromResult<object?>(new { ok = true }); }));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.False(result.IsSuccess);
        Assert.StartsWith("BUDGET_EXCEEDED:", result.ErrorMessage);
        Assert.Equal(8, invoked);
        Assert.Equal(8, result.TotalToolCalls);
    }

    [Fact]
    public async Task AgentLoop_MaxTotalToolCallsExceeded_WithinSingleTurn_HaltsImmediately()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Enumerable.Range(1, 9).Select(i => Call("get_title_details", ("bookId", i))).ToArray());
        var invoked = 0;
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => { invoked++; return Task.FromResult<object?>(new { ok = true }); }));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.StartsWith("BUDGET_EXCEEDED:", result.ErrorMessage);
        Assert.Equal(8, invoked);
    }

    [Fact]
    public async Task AgentLoop_RepeatCallLimitExceeded_WithPermutedArgumentKeys_HaltsWithRepeatCallError()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 1), ("format", "Ebook")));
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Ebook"), ("page", 1)));
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 1), ("format", "Ebook")));
        var invoked = 0;
        var provider = new StubToolProvider(Tool("find_titles_missing_metadata", (_, _, _, _) => { invoked++; return Task.FromResult<object?>(new { hasMore = false }); }));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.StartsWith("REPEAT_CALL_LIMIT:", result.ErrorMessage);
        Assert.Equal(2, invoked);
    }

    [Fact]
    public async Task AgentLoop_PayloadSizeCap_RejectsExcessivePayload()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 1)));
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => Task.FromResult<object?>(new { text = new string('x', 17_000) })));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.StartsWith("PAYLOAD_LIMIT_EXCEEDED:", result.ErrorMessage);
        Assert.Single(chat.Invocations);
        Assert.DoesNotContain(chat.Invocations[0].SelectMany(message => message.Contents), content => content is FunctionResultContent);
    }

    [Fact]
    public async Task AgentLoop_PaginationLimitExceeded_RejectsPageBeyondCap()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("page", 3)));
        var invoked = false;
        var provider = new StubToolProvider(Tool("find_titles_missing_metadata", (_, _, _, _) => { invoked = true; return Task.FromResult<object?>(new { }); }));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.StartsWith("PAGINATION_LIMIT_EXCEEDED:", result.ErrorMessage);
        Assert.False(invoked);

        var pagesChat = new ScriptedChatClient();
        pagesChat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Ebook"), ("page", 1)));
        pagesChat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Audiobook"), ("page", 1)));
        pagesChat.EnqueueFunctionCalls(Call("find_titles_missing_metadata", ("format", "Paperback"), ("page", 1)));
        var pageCalls = 0;
        var pagesProvider = new StubToolProvider(Tool("find_titles_missing_metadata", (_, _, _, _) =>
        {
            pageCalls++;
            return Task.FromResult<object?>(new { hasMore = false });
        }));
        var pagesResult = await Runner(pagesChat, pagesProvider).RunAsync("Question");
        Assert.StartsWith("PAGINATION_LIMIT_EXCEEDED:", pagesResult.ErrorMessage);
        Assert.Equal(2, pageCalls);
    }

    [Fact]
    public async Task AgentLoop_UnknownToolRequested_HaltsGracefullyWithUnknownToolError()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("delete_all_records"));
        var result = await Runner(chat, new StubToolProvider()).RunAsync("Question");
        Assert.StartsWith("UNKNOWN_TOOL:", result.ErrorMessage);
    }

    [Fact]
    public async Task AgentLoop_DuplicateMcpToolNames_HaltsWithInvalidToolsetError()
    {
        var provider = new StubToolProvider(
            Tool("find_titles_missing_metadata", (_, _, _, _) => Task.FromResult<object?>(new { })),
            Tool("find_titles_missing_metadata", (_, _, _, _) => Task.FromResult<object?>(new { })));
        var result = await Runner(new ScriptedChatClient(), provider).RunAsync("Question");
        Assert.StartsWith("INVALID_TOOLSET:", result.ErrorMessage);
    }

    [Fact]
    public async Task AgentLoop_ToolThrowsException_HaltsGracefullyWithMaskedError()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 1)));
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => throw new HttpRequestException("Sensitive database server address")));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.StartsWith("TOOL_ERROR:", result.ErrorMessage);
        Assert.DoesNotContain("Sensitive database server address", result.ErrorMessage);
    }

    [Fact]
    public async Task AgentLoop_ToolSerializationFails_HaltsWithSerializationError()
    {
        var cycle = new Dictionary<string, object?>();
        cycle["self"] = cycle;
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 1)));
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => Task.FromResult<object?>(cycle)));
        var result = await Runner(chat, provider).RunAsync("Question");
        Assert.StartsWith("SERIALIZATION_ERROR:", result.ErrorMessage);
    }

    [Fact]
    public async Task AgentLoop_ModelCallTimeout_CancelsAndFailsGracefully()
    {
        var chat = new ScriptedChatClient();
        chat.Enqueue(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "late"));
        });
        var result = await Runner(chat, new StubToolProvider(), perCall: 25, overall: 250).RunAsync("Question");
        Assert.StartsWith("TIMEOUT:", result.ErrorMessage);
        Assert.Contains("model call", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        using var callerCancellation = new CancellationTokenSource();
        var cancelledChat = new ScriptedChatClient();
        cancelledChat.Enqueue(async (_, cancellationToken) =>
        {
            callerCancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unreachable"));
        });
        var cancelled = await Runner(cancelledChat, new StubToolProvider()).RunAsync("Question", callerCancellation.Token);
        Assert.StartsWith("CANCELLED:", cancelled.ErrorMessage);
    }

    [Fact]
    public async Task AgentLoop_ToolCallTimeout_NonCooperativeTool_AbortsExecution()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 1)));
        var provider = new StubToolProvider(Tool("get_title_details", async (_, _, _, _) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return new { };
        }));
        var result = await Runner(chat, provider, perCall: 25, overall: 200).RunAsync("Question");
        Assert.StartsWith("TIMEOUT:", result.ErrorMessage);
        Assert.Contains("tool call", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentLoop_OverallDeadlineExceeded_HaltsExecution()
    {
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 1)));
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 2)));
        var provider = new StubToolProvider(Tool("get_title_details", async (_, _, _, cancellationToken) =>
        {
            await Task.Delay(70, cancellationToken);
            return new { ok = true };
        }));
        var result = await Runner(chat, provider, perCall: 90, overall: 120).RunAsync("Question");
        Assert.StartsWith("TIMEOUT:", result.ErrorMessage);
        Assert.Contains("overall deadline", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenTelemetry_EmitsSpansWithoutSensitiveData()
    {
        var captured = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(Call("get_title_details", ("bookId", 54321)));
        chat.EnqueueText("safe answer");
        var provider = new StubToolProvider(Tool("get_title_details", (_, _, _, _) => Task.FromResult<object?>(new { title = "private-output" })));
        _ = await Runner(chat, provider).RunAsync("my-secret-prompt");
        var root = Assert.Single(captured, activity => activity.OperationName == "agent.run");
        var modelTurns = captured.Where(activity => activity.OperationName == "agent.model_turn").ToArray();
        var toolCall = Assert.Single(captured, activity => activity.OperationName == "agent.tool_call");
        Assert.Equal(2, modelTurns.Length);
        Assert.Contains(root.TagObjects, tag => tag.Key == "agent.status" && Equals(tag.Value, "completed"));
        Assert.Contains(root.TagObjects, tag => tag.Key == "agent.duration_ms");
        Assert.All(modelTurns, modelTurn => Assert.Contains(modelTurn.TagObjects, tag => tag.Key == "agent.turn_number"));
        Assert.All(modelTurns, modelTurn => Assert.Contains(modelTurn.TagObjects, tag => tag.Key == "agent.duration_ms"));
        Assert.Contains(toolCall.TagObjects, tag => tag.Key == "agent.tool_name");
        Assert.Contains(toolCall.TagObjects, tag => tag.Key == "agent.payload_bytes");
        var tags = captured.SelectMany(activity => activity.TagObjects).Select(tag => tag.Value?.ToString() ?? string.Empty).ToArray();
        Assert.DoesNotContain(tags, value => value.Contains("my-secret-prompt", StringComparison.Ordinal));
        Assert.DoesNotContain(tags, value => value.Contains("54321", StringComparison.Ordinal));
        Assert.DoesNotContain(tags, value => value.Contains("private-output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PerRequestBearerHandler_AttachesToken_ReplacesExistingHeader_AndRejectsInsecureEndpoints()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"));
        context.Request.Headers.Authorization = "Bearer caller-token";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var recorder = new CapturingHandler();
        using var handler = new PerRequestBearerHandler(accessor, Options.Create(new AIProviderOptions { McpEndpoint = "https://trusted.example/mcp" }), Environment("Production"))
        {
            InnerHandler = recorder
        };
        using (var client = new HttpClient(handler))
        using (var request = new HttpRequestMessage(HttpMethod.Post, "https://trusted.example/mcp"))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "malicious");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Bearer caller-token", recorder.Authorizations[^1]);

            context.Request.Headers.Authorization = "Bearer second-caller-token";
            using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "https://trusted.example/mcp");
            using var secondResponse = await client.SendAsync(secondRequest);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.Equal(new[] { "Bearer caller-token", "Bearer second-caller-token" }, recorder.Authorizations);
        }

        var insecureHandler = new PerRequestBearerHandler(accessor, Options.Create(new AIProviderOptions { McpEndpoint = "http://external.example/mcp" }), Environment("Production"))
        {
            InnerHandler = new CapturingHandler()
        };
        using var insecureClient = new HttpClient(insecureHandler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => insecureClient.GetAsync("http://external.example/mcp"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => insecureClient.GetAsync("https://other.example/mcp"));
    }

    [Fact]
    public async Task AIProviderSetup_RoutingAndHeaderTests()
    {
        foreach (var provider in new[] { "openrouter", "ollama", "openai" })
        {
            var config = new AIProviderOptions { Type = provider, ApiKey = "test-key", OpenRouterApiKey = "test-key" };
            var resolved = AIProviderSetup.Resolve(config, _ => null);
            Assert.Equal(provider, resolved.Type);
            Assert.NotNull(resolved.Endpoint);
            var expectedHost = provider switch
            {
                "openrouter" => "openrouter.ai",
                "ollama" => "localhost",
                _ => "api.openai.com"
            };
            Assert.Equal(expectedHost, new Uri(resolved.Endpoint).Host);
            var recording = new RecordingHttpMessageHandler();
            using var http = new HttpClient(recording);
            using var chat = AIProviderSetup.CreateChatClient(resolved, http);
            _ = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
            Assert.Single(recording.Requests);
            Assert.Contains("chat/completions", recording.Requests[0].RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            if (provider == "openrouter")
            {
                Assert.Contains("PublishingTracker", recording.Requests[0].Headers.GetValues("X-Title"));
                Assert.Contains("https://publishingtracker.local", recording.Requests[0].Headers.GetValues("HTTP-Referer"));
                Assert.Contains("deny", recording.Requests[0].Headers.GetValues("data_collection"));
            }
        }
        Assert.Throws<InvalidOperationException>(() => AIProviderSetup.Resolve(new AIProviderOptions { Type = "azureopenai" }, _ => null));
        Assert.Throws<InvalidOperationException>(() => new AgentLoopOptions { MaxTotalToolCalls = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AgentLoopOptions { OverallTimeout = TimeSpan.FromSeconds(20) }.Validate());
    }

    [Fact]
    public void AIProviderOptions_ToString_RedactsApiKeys()
    {
        var options = new AIProviderOptions { ApiKey = "secret-api-key", OpenRouterApiKey = "other-secret" };
        var text = options.ToString();
        Assert.Contains("[REDACTED]", text);
        Assert.DoesNotContain("secret-api-key", text);
        Assert.DoesNotContain("other-secret", text);
        var serialized = JsonSerializer.Serialize(options);
        Assert.DoesNotContain("secret-api-key", serialized);
        Assert.DoesNotContain("other-secret", serialized);
    }

    private static AgentLoopRunner Runner(ScriptedChatClient chat, IMcpToolProvider provider, int perCall = 1_000, int overall = 5_000) =>
        new(chat, provider, Options.Create(new AgentLoopOptions
        {
            PerCallTimeout = TimeSpan.FromMilliseconds(perCall),
            OverallTimeout = TimeSpan.FromMilliseconds(overall)
        }), NullLogger<AgentLoopRunner>.Instance);

    private static AIFunction Tool(string name, Func<int, string?, int, CancellationToken, Task<object?>> operation) =>
        StubToolProvider.CreateTool(name, operation);

    private static (string Name, IDictionary<string, object?> Arguments) Call(string name, params (string Key, object? Value)[] values) =>
        (name, values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));

    private static IHostEnvironment Environment(string environmentName) => new TestHostEnvironment(environmentName);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PublishingTracker.Agent.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
