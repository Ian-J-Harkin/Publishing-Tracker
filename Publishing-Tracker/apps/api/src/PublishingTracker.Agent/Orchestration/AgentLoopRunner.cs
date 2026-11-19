using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Telemetry;
using PublishingTracker.Agent.Validation;

namespace PublishingTracker.Agent.Orchestration;

public sealed class AgentLoopRunner
{
    private readonly IChatClient _chatClient;
    private readonly IMcpToolProvider _toolProvider;
    private readonly AgentLoopOptions _options;
    private readonly AgentOutputValidator _validator;
    private readonly ILogger<AgentLoopRunner> _logger;

    public AgentLoopRunner(
        IChatClient chatClient,
        IMcpToolProvider toolProvider,
        IOptions<AgentLoopOptions> options,
        ILogger<AgentLoopRunner> logger,
        AgentOutputValidator? validator = null)
    {
        _chatClient = chatClient;
        _toolProvider = toolProvider;
        _options = options.Value;
        _options.Validate();
        _validator = validator ?? new AgentOutputValidator();
        _logger = logger;
    }

    /// <summary>Phase 2 explicit dual-budget orchestration loop returning Stage 1 text completion.</summary>
    public async Task<AgentRunResult> RunAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var state = await ExecuteStage1Async(userPrompt, strictEvidenceMode: false, cancellationToken);
        try
        {
            if (!state.Succeeded)
            {
                var errorMessage = state.Phase2TimeoutDetail is not null && state.ErrorCode == AgentErrorCode.TIMEOUT
                    ? state.Phase2TimeoutDetail
                    : AgentErrorCatalog.GetPublicMessage(state.ErrorCode);
                return AgentRunResult.Failed(
                    errorMessage,
                    state.ToolsUsedNames,
                    state.ModelTurns,
                    state.TotalToolCalls);
            }

            state.RunActivity?.SetTag("agent.total_tool_calls", state.TotalToolCalls);
            state.RunActivity?.SetTag("agent.status", "completed");
            state.RunActivity?.SetTag("agent.duration_ms", state.Stopwatch.ElapsedMilliseconds);

            return AgentRunResult.Completed(
                state.FinalStage1Text!,
                state.ToolsUsedNames,
                state.ModelTurns,
                state.TotalToolCalls,
                state.Phase2Limitations);
        }
        finally
        {
            await state.DisposeAsync();
        }
    }

    /// <summary>Phase 3 two-stage orchestration with EvidenceLedger provenance and schema repair.</summary>
    public async Task<AgentQueryResponse> RunVerifiedAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        var state = await ExecuteStage1Async(userPrompt, strictEvidenceMode: true, cancellationToken);
        try
        {
            if (!state.Succeeded)
            {
                return AgentQueryResponse.Failure(
                    state.ErrorCode,
                    state.Ledger.ToolsUsed,
                    state.ModelTurns,
                    state.TotalToolCalls,
                    state.Ledger.HasPaginationTruncation);
            }

            return await ExecuteStage2Async(state, cancellationToken);
        }
        finally
        {
            await state.DisposeAsync();
        }
    }

    private async Task<LoopRunState> ExecuteStage1Async(
        string userPrompt,
        bool strictEvidenceMode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);
        var state = new LoopRunState(
            Stopwatch.StartNew(),
            AgentTelemetry.Source.StartActivity("agent.run"),
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));

        state.RunActivity?.SetTag("agent.max_turns", _options.MaxModelTurns);
        state.RunActivity?.SetTag("agent.max_tool_calls", _options.MaxTotalToolCalls);
        state.RunCts.CancelAfter(_options.OverallTimeout);
        var runToken = state.RunCts.Token;

        IMcpToolSession session;
        try
        {
            session = await _toolProvider.OpenSessionAsync(runToken);
            state.Session = session;
        }
        catch (OperationCanceledException)
        {
            var code = cancellationToken.IsCancellationRequested ? AgentErrorCode.CANCELLED : AgentErrorCode.TIMEOUT;
            return FailState(state, code, "TIMEOUT: The request exceeded its overall deadline.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "MCP tool discovery failed.");
            return FailState(state, AgentErrorCode.TOOL_DISCOVERY_ERROR);
        }

        var toolMap = new Dictionary<string, AIFunction>(StringComparer.Ordinal);
        foreach (var tool in session.Tools)
        {
            if (!toolMap.TryAdd(tool.Name, tool))
                return FailState(state, AgentErrorCode.INVALID_TOOLSET);
        }

        state.Messages.Add(new ChatMessage(ChatRole.System, AgentPrompts.SystemPromptV1));
        state.Messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var chatOptions = new ChatOptions { Tools = [.. session.Tools] };
        var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var paginationState = new Dictionary<string, (int Page, bool HasMore)>(StringComparer.Ordinal);
        var cataloguePagesUsed = 0;

        for (var turn = 1; turn <= _options.MaxModelTurns; turn++)
        {
            state.ModelTurns = turn;
            var turnWatch = Stopwatch.StartNew();
            using var turnActivity = AgentTelemetry.Source.StartActivity("agent.model_turn");
            turnActivity?.SetTag("agent.turn_number", turn);
            if (strictEvidenceMode)
                turnActivity?.SetTag("agent.stage", "tool_selection");

            using var modelCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
            modelCts.CancelAfter(_options.PerCallTimeout);

            ChatResponse response;
            try
            {
                response = await _chatClient.GetResponseAsync(state.Messages, chatOptions, modelCts.Token);
                state.Messages.AddRange(response.Messages);
                turnActivity?.SetTag("agent.duration_ms", turnWatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                var code = cancellationToken.IsCancellationRequested ? AgentErrorCode.CANCELLED : AgentErrorCode.TIMEOUT;
                var detail = runToken.IsCancellationRequested
                    ? "TIMEOUT: The request exceeded its overall deadline."
                    : "TIMEOUT: The model call exceeded its per-call timeout.";
                turnActivity?.SetTag("agent.error_code", code.ToString());
                return FailState(state, code, detail);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Model invocation failed on turn {Turn}.", turn);
                turnActivity?.SetTag("agent.error_code", nameof(AgentErrorCode.MODEL_ERROR));
                return FailState(state, AgentErrorCode.MODEL_ERROR);
            }

            var functionCalls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToArray();
            turnActivity?.SetTag("agent.has_function_calls", functionCalls.Length != 0);
            if (functionCalls.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(response.Text))
                    return FailState(state, AgentErrorCode.EMPTY_RESPONSE);

                state.Phase2Limitations = paginationState.Values.Any(entry => entry.HasMore)
                    ? ["Catalogue results remain incomplete because the last fetched page indicated more results."]
                    : Array.Empty<string>();
                state.FinalStage1Text = response.Text.Trim();
                state.Succeeded = true;
                return state;
            }

            var maxStage1Turns = strictEvidenceMode ? Math.Max(1, _options.MaxModelTurns - 1) : _options.MaxModelTurns;
            if (turn >= maxStage1Turns)
                return FailState(state, AgentErrorCode.TURN_LIMIT_EXCEEDED);

            foreach (var call in functionCalls)
            {
                if (state.TotalToolCalls >= _options.MaxTotalToolCalls)
                    return FailState(state, AgentErrorCode.BUDGET_EXCEEDED);

                state.TotalToolCalls++;
                state.RunActivity?.SetTag("agent.total_tool_calls", state.TotalToolCalls);

                if (!toolMap.TryGetValue(call.Name, out var tool))
                    return FailState(state, AgentErrorCode.UNKNOWN_TOOL);

                string signature;
                try
                {
                    signature = CanonicalizeSignature(call.Name, call.Arguments);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Tool arguments could not be canonicalized.");
                    return FailState(state, AgentErrorCode.INVALID_TOOL_ARGUMENTS);
                }

                var callCount = callCounts.GetValueOrDefault(signature) + 1;
                callCounts[signature] = callCount;
                if (callCount > _options.RepeatCallLimit)
                    return FailState(state, AgentErrorCode.REPEAT_CALL_LIMIT);

                var page = 1;
                if (strictEvidenceMode)
                {
                    if (!state.Ledger.TryValidatePaginationRequest(
                        call.Name, call.Arguments, _options.MaxPaginationPages, out var pageErrorCode, out var pageDiagnostic))
                    {
                        _logger.LogWarning("Pagination pre-check failed for {ToolName}: {Diagnostic}", call.Name, pageDiagnostic);
                        return FailState(state, pageErrorCode);
                    }
                }
                else if (call.Name == "find_titles_missing_metadata")
                {
                    if (!TryExtractPage(call.Arguments, out page))
                        return FailState(state, AgentErrorCode.INVALID_TOOL_ARGUMENTS);
                    if (page < 1 || page > _options.MaxPaginationPages || cataloguePagesUsed >= _options.MaxPaginationPages)
                        return FailState(state, AgentErrorCode.PAGINATION_LIMIT_EXCEEDED);
                    cataloguePagesUsed++;
                }

                if (!state.ToolsUsedNames.Contains(call.Name, StringComparer.Ordinal))
                    state.ToolsUsedNames.Add(call.Name);

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
                        state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: false);
                        var code = cancellationToken.IsCancellationRequested ? AgentErrorCode.CANCELLED : AgentErrorCode.TIMEOUT;
                        var detail = runToken.IsCancellationRequested
                            ? "TIMEOUT: The request exceeded its overall deadline."
                            : "TIMEOUT: The tool call exceeded its per-call timeout.";
                        return FailState(state, code, detail);
                    }

                    result = await invokeTask;
                    toolActivity?.SetTag("agent.duration_ms", toolWatch.ElapsedMilliseconds);
                    toolActivity?.SetTag("agent.is_error", false);
                }
                catch (OperationCanceledException)
                {
                    toolActivity?.SetTag("agent.is_error", true);
                    state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: false);
                    var code = cancellationToken.IsCancellationRequested ? AgentErrorCode.CANCELLED : AgentErrorCode.TIMEOUT;
                    var detail = runToken.IsCancellationRequested
                        ? "TIMEOUT: The request exceeded its overall deadline."
                        : "TIMEOUT: The tool call exceeded its per-call timeout.";
                    return FailState(state, code, detail);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "MCP tool invocation failed for {ToolName}.", call.Name);
                    toolActivity?.SetTag("agent.is_error", true);
                    state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: false);
                    return FailState(state, AgentErrorCode.TOOL_ERROR);
                }

                string serialized;
                try
                {
                    serialized = JsonSerializer.Serialize(result, JsonSerializerOptions.Web);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "MCP tool output serialization failed for {ToolName}.", call.Name);
                    state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: false);
                    return FailState(state, AgentErrorCode.SERIALIZATION_ERROR);
                }

                var payloadBytes = Encoding.UTF8.GetByteCount(serialized);
                toolActivity?.SetTag("agent.payload_bytes", payloadBytes);
                if (payloadBytes > _options.MaxToolPayloadBytes)
                {
                    state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: false);
                    return FailState(state, AgentErrorCode.PAYLOAD_LIMIT_EXCEEDED);
                }

                if (strictEvidenceMode)
                {
                    if (!state.Ledger.TryRecordToolOutput(
                        call.Name, call.Arguments, result, serialized, out var indexErrorCode, out var indexDiagnostic))
                    {
                        _logger.LogWarning("Evidence indexing failed for {ToolName}: {Diagnostic}", call.Name, indexDiagnostic);
                        state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: false);
                        return FailState(state, indexErrorCode);
                    }

                    state.Ledger.RecordToolExecution(call.Name, turn, toolWatch.ElapsedMilliseconds, succeeded: true);
                }
                else
                {
                    if (call.Name == "find_titles_missing_metadata")
                        paginationState[CreatePaginationQuerySignature(call)] = (page, ReadHasMore(result, serialized));
                }

                state.Messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(call.CallId, serialized)]));
            }
        }

        return FailState(state, AgentErrorCode.TURN_LIMIT_EXCEEDED);
    }

    private async Task<AgentQueryResponse> ExecuteStage2Async(LoopRunState state, CancellationToken cancellationToken)
    {
        var runToken = state.RunCts.Token;

        // Stage 1 ended with a ChatRole.Assistant message in state.Messages.
        // Append the Stage 2 User formatting instruction once before attempt = 0.
        state.Messages.Add(new ChatMessage(ChatRole.User, AgentPrompts.Stage2FormattingPrompt));

        var schemaOptions = new ChatOptions
        {
            Tools = null,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<AgentModelDraft>(JsonSerializerOptions.Web)
        };

        for (var attempt = 0; attempt <= _options.MaxValidationRetries; attempt++)
        {
            if (state.ModelTurns >= _options.MaxModelTurns)
            {
                return FinishVerifiedFailure(state, AgentErrorCode.TURN_LIMIT_EXCEEDED);
            }

            state.ModelTurns++;
            var stage2Watch = Stopwatch.StartNew();
            using var turnActivity = AgentTelemetry.Source.StartActivity("agent.model_turn");
            turnActivity?.SetTag("agent.turn_number", state.ModelTurns);
            turnActivity?.SetTag("agent.stage", "structured_output");
            turnActivity?.SetTag("agent.validation_attempt", attempt);

            using var stage2CallCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
            stage2CallCts.CancelAfter(_options.PerCallTimeout);

            ChatResponse stage2Response;
            try
            {
                stage2Response = await _chatClient.GetResponseAsync(state.Messages, schemaOptions, stage2CallCts.Token);
                turnActivity?.SetTag("agent.duration_ms", stage2Watch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                var code = cancellationToken.IsCancellationRequested ? AgentErrorCode.CANCELLED : AgentErrorCode.TIMEOUT;
                turnActivity?.SetTag("agent.error_code", code.ToString());
                return FinishVerifiedFailure(state, code);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Stage 2 structured model call failed on turn {Turn}, attempt {Attempt}.", state.ModelTurns, attempt);
                turnActivity?.SetTag("agent.error_code", nameof(AgentErrorCode.MODEL_ERROR));
                return FinishVerifiedFailure(state, AgentErrorCode.MODEL_ERROR);
            }

            var validation = _validator.Validate(stage2Response.Text, state.Ledger);
            turnActivity?.SetTag("agent.validation_passed", validation.IsValid);

            if (validation.IsValid && validation.Draft is not null)
            {
                state.RunActivity?.SetTag("agent.validation_retries_used", attempt);
                state.RunActivity?.SetTag("agent.status", "completed");
                state.RunActivity?.SetTag("agent.total_tool_calls", state.TotalToolCalls);
                state.RunActivity?.SetTag("agent.duration_ms", state.Stopwatch.ElapsedMilliseconds);

                return AgentQueryResponse.Answered(
                    validation.Draft,
                    state.Ledger.HasPaginationTruncation,
                    state.Ledger.ToolsUsed,
                    state.ModelTurns,
                    state.TotalToolCalls);
            }

            if (attempt == _options.MaxValidationRetries)
            {
                _logger.LogWarning("Stage 2 validation exhausted retries: {ValidationError}", validation.Error);
                return FinishVerifiedFailure(state, AgentErrorCode.VALIDATION_RETRIES_EXHAUSTED);
            }

            // Preserve alternating User -> Assistant -> User conversation ordering on repair retry.
            state.Messages.Add(new ChatMessage(ChatRole.Assistant, stage2Response.Text ?? string.Empty));
            state.Messages.Add(new ChatMessage(ChatRole.User, AgentPrompts.BuildStage2RepairPrompt(validation.Error!)));
        }

        return FinishVerifiedFailure(state, AgentErrorCode.VALIDATION_RETRIES_EXHAUSTED);
    }

    private static LoopRunState FailState(LoopRunState state, AgentErrorCode errorCode, string? phase2TimeoutDetail = null)
    {
        state.Succeeded = false;
        state.ErrorCode = errorCode;
        state.Phase2TimeoutDetail = errorCode == AgentErrorCode.TIMEOUT ? phase2TimeoutDetail : null;
        state.RunActivity?.SetTag("agent.total_tool_calls", state.TotalToolCalls);
        state.RunActivity?.SetTag("agent.status", "failed");
        state.RunActivity?.SetTag("agent.error_code", errorCode.ToString());
        state.RunActivity?.SetTag("agent.duration_ms", state.Stopwatch.ElapsedMilliseconds);
        return state;
    }

    private static AgentQueryResponse FinishVerifiedFailure(LoopRunState state, AgentErrorCode errorCode)
    {
        FailState(state, errorCode);
        return AgentQueryResponse.Failure(
            errorCode,
            state.Ledger.ToolsUsed,
            state.ModelTurns,
            state.TotalToolCalls,
            state.Ledger.HasPaginationTruncation);
    }

    private sealed class LoopRunState(Stopwatch stopwatch, Activity? runActivity, CancellationTokenSource runCts) : IAsyncDisposable
    {
        public Stopwatch Stopwatch { get; } = stopwatch;
        public Activity? RunActivity { get; } = runActivity;
        public CancellationTokenSource RunCts { get; } = runCts;
        public IMcpToolSession? Session { get; set; }
        public EvidenceLedger Ledger { get; } = new();
        public List<ChatMessage> Messages { get; } = [];
        public List<string> ToolsUsedNames { get; } = [];
        public IReadOnlyList<string> Phase2Limitations { get; set; } = Array.Empty<string>();
        public string? FinalStage1Text { get; set; }
        public int ModelTurns { get; set; }
        public int TotalToolCalls { get; set; }
        public bool Succeeded { get; set; }
        public AgentErrorCode ErrorCode { get; set; }
        public string? Phase2TimeoutDetail { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (Session is not null)
            {
                await Session.DisposeAsync();
                Session = null;
            }
            RunCts.Dispose();
            RunActivity?.Dispose();
        }
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
