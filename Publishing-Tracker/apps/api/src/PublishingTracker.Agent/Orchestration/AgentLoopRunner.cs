using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Telemetry;

namespace PublishingTracker.Agent.Orchestration;

public sealed class AgentLoopRunner
{
    private readonly IChatClient _chatClient;
    private readonly IMcpToolProvider _toolProvider;
    private readonly AgentLoopOptions _options;
    private readonly ILogger<AgentLoopRunner> _logger;

    public AgentLoopRunner(IChatClient chatClient, IMcpToolProvider toolProvider, IOptions<AgentLoopOptions> options, ILogger<AgentLoopRunner> logger)
    {
        _chatClient = chatClient;
        _toolProvider = toolProvider;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    public async Task<AgentRunResult> RunAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);
        var stopwatch = Stopwatch.StartNew();
        using var runActivity = AgentTelemetry.Source.StartActivity("agent.run");
        runActivity?.SetTag("agent.max_turns", _options.MaxModelTurns);
        runActivity?.SetTag("agent.max_tool_calls", _options.MaxTotalToolCalls);
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(_options.OverallTimeout);
        var runToken = runCts.Token;

        var toolsUsed = new List<string>();
        var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var paginationState = new Dictionary<string, (int Page, bool HasMore)>(StringComparer.Ordinal);
        var totalToolCalls = 0;
        var cataloguePagesUsed = 0;
        var modelTurns = 0;

        IMcpToolSession session;
        try
        {
            session = await _toolProvider.OpenSessionAsync(runToken);
        }
        catch (OperationCanceledException)
        {
            return FinishFailure(cancellationToken.IsCancellationRequested ? "CANCELLED" : "TIMEOUT",
                "The request was cancelled or exceeded its overall deadline.", toolsUsed, modelTurns, totalToolCalls, runActivity, stopwatch);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "MCP tool discovery failed.");
            return FinishFailure("TOOL_DISCOVERY_ERROR", "MCP tools could not be loaded.", toolsUsed, modelTurns, totalToolCalls, runActivity, stopwatch);
        }

        await using var ownedSession = session;
        var toolMap = new Dictionary<string, AIFunction>(StringComparer.Ordinal);
        foreach (var tool in session.Tools)
        {
            if (!toolMap.TryAdd(tool.Name, tool))
                return FinishFailure("INVALID_TOOLSET", "The MCP server returned duplicate tool names.", toolsUsed, modelTurns, totalToolCalls, runActivity, stopwatch);
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AgentPrompts.SystemPromptV1),
            new(ChatRole.User, userPrompt)
        };
        var chatOptions = new ChatOptions { Tools = [.. session.Tools] };

        for (var turn = 1; turn <= _options.MaxModelTurns; turn++)
        {
            modelTurns = turn;
            var turnWatch = Stopwatch.StartNew();
            using var turnActivity = AgentTelemetry.Source.StartActivity("agent.model_turn");
            turnActivity?.SetTag("agent.turn_number", turn);
            using var modelCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
            modelCts.CancelAfter(_options.PerCallTimeout);

            ChatResponse response;
            try
            {
                response = await _chatClient.GetResponseAsync(messages, chatOptions, modelCts.Token);
                messages.AddRange(response.Messages);
                turnActivity?.SetTag("agent.duration_ms", turnWatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                var code = cancellationToken.IsCancellationRequested ? "CANCELLED" :
                    runToken.IsCancellationRequested ? "TIMEOUT" : "TIMEOUT";
                var message = cancellationToken.IsCancellationRequested ? "The request was cancelled by the caller." :
                    runToken.IsCancellationRequested ? "The request exceeded its overall deadline." : "The model call exceeded its per-call timeout.";
                turnActivity?.SetTag("agent.error_code", code);
                return FinishFailure(code, message, toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Model invocation failed on turn {Turn}.", turn);
                turnActivity?.SetTag("agent.error_code", "MODEL_ERROR");
                return FinishFailure("MODEL_ERROR", "The model call failed.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
            }

            var functionCalls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToArray();
            turnActivity?.SetTag("agent.has_function_calls", functionCalls.Length != 0);
            if (functionCalls.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(response.Text))
                    return FinishFailure("EMPTY_RESPONSE", "The model returned an empty final answer.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);

                var limitations = paginationState.Values.Any(state => state.HasMore)
                    ? ["Catalogue results remain incomplete because the last fetched page indicated more results."]
                    : Array.Empty<string>();
                runActivity?.SetTag("agent.total_tool_calls", totalToolCalls);
                runActivity?.SetTag("agent.status", "completed");
                runActivity?.SetTag("agent.duration_ms", stopwatch.ElapsedMilliseconds);
                return AgentRunResult.Completed(response.Text.Trim(), toolsUsed.ToArray(), turn, totalToolCalls, limitations);
            }

            if (turn == _options.MaxModelTurns)
                return FinishFailure("TURN_LIMIT_EXCEEDED", "The maximum model-turn budget was reached before the requested tools could run.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);

            foreach (var call in functionCalls)
            {
                if (totalToolCalls >= _options.MaxTotalToolCalls)
                    return FinishFailure("BUDGET_EXCEEDED", "The maximum tool-call budget was exceeded.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                totalToolCalls++;
                runActivity?.SetTag("agent.total_tool_calls", totalToolCalls);

                if (!toolMap.TryGetValue(call.Name, out var tool))
                    return FinishFailure("UNKNOWN_TOOL", "The model requested a tool that is not available.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);

                string signature;
                try
                {
                    signature = CanonicalizeSignature(call.Name, call.Arguments);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Tool arguments could not be canonicalized.");
                    return FinishFailure("INVALID_TOOL_ARGUMENTS", "The tool arguments could not be processed.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                }
                var callCount = callCounts.GetValueOrDefault(signature) + 1;
                callCounts[signature] = callCount;
                if (callCount > _options.RepeatCallLimit)
                    return FinishFailure("REPEAT_CALL_LIMIT", "An identical tool call exceeded its repeat limit.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);

                var page = 1;
                if (call.Name == "find_titles_missing_metadata")
                {
                    if (!TryExtractPage(call.Arguments, out page))
                        return FinishFailure("INVALID_TOOL_ARGUMENTS", "The requested catalogue page argument must be a valid integer.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                    if (page < 1 || page > _options.MaxPaginationPages || cataloguePagesUsed >= _options.MaxPaginationPages)
                        return FinishFailure("PAGINATION_LIMIT_EXCEEDED", "The requested catalogue page exceeds the configured page limit.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                    cataloguePagesUsed++;
                }

                if (!toolsUsed.Contains(call.Name, StringComparer.Ordinal)) toolsUsed.Add(call.Name);
                var toolWatch = Stopwatch.StartNew();
                using var toolActivity = AgentTelemetry.Source.StartActivity("agent.tool_call");
                toolActivity?.SetTag("agent.tool_name", call.Name);
                using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
                toolCts.CancelAfter(_options.PerCallTimeout);
                object? result;
                try
                {
                    var arguments = call.Arguments is null ? new AIFunctionArguments() : new AIFunctionArguments(call.Arguments);
                    var invokeTask = tool.InvokeAsync(arguments, toolCts.Token).AsTask();
                    var perCallDelay = Task.Delay(_options.PerCallTimeout, CancellationToken.None);
                    var overallCancellation = Task.Delay(Timeout.InfiniteTimeSpan, runToken);
                    var completed = await Task.WhenAny(invokeTask, perCallDelay, overallCancellation);
                    if (completed != invokeTask)
                    {
                        _ = invokeTask.ContinueWith(task => _ = task.Exception, CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        toolActivity?.SetTag("agent.is_error", true);
                        if (cancellationToken.IsCancellationRequested)
                            return FinishFailure("CANCELLED", "The request was cancelled by the caller.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                        if (runToken.IsCancellationRequested)
                            return FinishFailure("TIMEOUT", "The request exceeded its overall deadline.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                        return FinishFailure("TIMEOUT", "The tool call exceeded its per-call timeout.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                    }
                    result = await invokeTask;
                    toolActivity?.SetTag("agent.duration_ms", toolWatch.ElapsedMilliseconds);
                    toolActivity?.SetTag("agent.is_error", false);
                }
                catch (OperationCanceledException)
                {
                    toolActivity?.SetTag("agent.is_error", true);
                    var code = cancellationToken.IsCancellationRequested ? "CANCELLED" : runToken.IsCancellationRequested ? "TIMEOUT" : "TIMEOUT";
                    var message = code == "CANCELLED" ? "The request was cancelled by the caller." :
                        runToken.IsCancellationRequested ? "The request exceeded its overall deadline." : "The tool call exceeded its per-call timeout.";
                    return FinishFailure(code, message, toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "MCP tool invocation failed for {ToolName}.", call.Name);
                    toolActivity?.SetTag("agent.is_error", true);
                    return FinishFailure("TOOL_ERROR", "A catalogue tool failed.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                }

                string serialized;
                try
                {
                    serialized = JsonSerializer.Serialize(result, JsonSerializerOptions.Web);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "MCP tool output serialization failed for {ToolName}.", call.Name);
                    return FinishFailure("SERIALIZATION_ERROR", "A catalogue tool result could not be serialized.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);
                }

                var payloadBytes = Encoding.UTF8.GetByteCount(serialized);
                toolActivity?.SetTag("agent.payload_bytes", payloadBytes);
                if (payloadBytes > _options.MaxToolPayloadBytes)
                    return FinishFailure("PAYLOAD_LIMIT_EXCEEDED", "A catalogue tool result exceeded the allowed payload size.", toolsUsed, turn, totalToolCalls, runActivity, stopwatch);

                if (call.Name == "find_titles_missing_metadata")
                    paginationState[CreatePaginationQuerySignature(call)] = (page, ReadHasMore(result, serialized));
                messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, serialized)]));
            }
        }

        return FinishFailure("TURN_LIMIT_EXCEEDED", "The maximum model-turn budget was reached.", toolsUsed, modelTurns, totalToolCalls, runActivity, stopwatch);
    }

    private static AgentRunResult FinishFailure(string code, string message, IReadOnlyList<string> toolsUsed, int turns, int toolCalls, Activity? activity, Stopwatch stopwatch)
    {
        activity?.SetTag("agent.total_tool_calls", toolCalls);
        activity?.SetTag("agent.status", "failed");
        activity?.SetTag("agent.error_code", code);
        activity?.SetTag("agent.duration_ms", stopwatch.ElapsedMilliseconds);
        return AgentRunResult.Failed($"{code}: {message}", toolsUsed.ToArray(), turns, toolCalls);
    }

    private static string CanonicalizeSignature(string name, IDictionary<string, object?>? arguments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WritePropertyName("arguments");
            WriteCanonical(writer, arguments ?? new Dictionary<string, object?>());
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, object? value)
    {
        if (value is JsonElement element)
        {
            WriteCanonicalElement(writer, element);
            return;
        }
        switch (value)
        {
            case null: writer.WriteNullValue(); return;
            case string text: writer.WriteStringValue(text); return;
            case bool boolean: writer.WriteBooleanValue(boolean); return;
            case byte number: writer.WriteNumberValue(number); return;
            case sbyte number: writer.WriteNumberValue(number); return;
            case short number: writer.WriteNumberValue(number); return;
            case ushort number: writer.WriteNumberValue(number); return;
            case int number: writer.WriteNumberValue(number); return;
            case uint number: writer.WriteNumberValue(number); return;
            case long number: writer.WriteNumberValue(number); return;
            case ulong number: writer.WriteNumberValue(number); return;
            case float number: writer.WriteNumberValue(number); return;
            case double number: writer.WriteNumberValue(number); return;
            case decimal number: writer.WriteNumberValue(number); return;
            case IDictionary<string, object?> dictionary:
                writer.WriteStartObject();
                foreach (var pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(pair.Key);
                    WriteCanonical(writer, pair.Value);
                }
                writer.WriteEndObject();
                return;
            case System.Collections.IEnumerable sequence when value is not string:
                writer.WriteStartArray();
                foreach (var item in sequence) WriteCanonical(writer, item);
                writer.WriteEndArray();
                return;
            default:
                WriteCanonicalElement(writer, JsonSerializer.SerializeToElement(value));
                return;
        }
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray()) WriteCanonicalElement(writer, child);
                writer.WriteEndArray();
                break;
            default: element.WriteTo(writer); break;
        }
    }

    private static bool TryExtractPage(IDictionary<string, object?>? arguments, out int page)
    {
        page = 1;
        if (arguments is null || !arguments.TryGetValue("page", out var raw) || raw is null) return true;
        if (raw is int intPage)
        {
            page = intPage;
            return true;
        }
        if (raw is long longPage && longPage is >= int.MinValue and <= int.MaxValue)
        {
            page = (int)longPage;
            return true;
        }
        if (raw is JsonElement element)
            return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out page);
        return raw is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out page);
    }

    private static string CreatePaginationQuerySignature(FunctionCallContent call)
    {
        var queryArguments = call.Arguments is null
            ? new Dictionary<string, object?>()
            : call.Arguments.Where(pair => !pair.Key.Equals("page", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return CanonicalizeSignature(call.Name, queryArguments);
    }

    private static bool ReadHasMore(object? result, string serialized)
    {
        if (result is not null)
        {
            var property = result.GetType().GetProperty("HasMore");
            if (property?.GetValue(result) is bool value) return value;
        }
        try
        {
            using var document = JsonDocument.Parse(serialized);
            var root = document.RootElement;
            if (TryReadHasMoreProperty(root, out var direct)) return direct;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("structuredContent", out var structured) &&
                TryReadHasMoreProperty(structured, out var structuredFlag))
                return structuredFlag;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Array &&
                content.GetArrayLength() > 0 &&
                content[0].ValueKind == JsonValueKind.Object &&
                content[0].TryGetProperty("text", out var textBlock) &&
                textBlock.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(textBlock.GetString()))
            {
                using var inner = JsonDocument.Parse(textBlock.GetString()!);
                if (TryReadHasMoreProperty(inner.RootElement, out var innerFlag)) return innerFlag;
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadHasMoreProperty(JsonElement element, out bool hasMore)
    {
        hasMore = false;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("hasMore", out var value))
            return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        hasMore = value.GetBoolean();
        return true;
    }
}
