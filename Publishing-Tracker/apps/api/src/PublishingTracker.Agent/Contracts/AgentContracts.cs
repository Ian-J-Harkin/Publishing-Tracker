using System.Text.Json.Serialization;

namespace PublishingTracker.Agent.Contracts;

public enum AgentErrorCode
{
    CANCELLED,
    TIMEOUT,
    TOOL_DISCOVERY_ERROR,
    INVALID_TOOLSET,
    MODEL_ERROR,
    EMPTY_RESPONSE,
    TURN_LIMIT_EXCEEDED,
    BUDGET_EXCEEDED,
    UNKNOWN_TOOL,
    INVALID_TOOL_ARGUMENTS,
    REPEAT_CALL_LIMIT,
    PAGINATION_LIMIT_EXCEEDED,
    PAGINATION_SEQUENCE_ERROR,
    TOOL_ERROR,
    SERIALIZATION_ERROR,
    PAYLOAD_LIMIT_EXCEEDED,
    EVIDENCE_INDEXING_ERROR,
    VALIDATION_RETRIES_EXHAUSTED
}

internal static class AgentErrorCatalog
{
    private static readonly IReadOnlyDictionary<AgentErrorCode, string> PublicMessages =
        new Dictionary<AgentErrorCode, string>
        {
            [AgentErrorCode.CANCELLED] = "CANCELLED: The request was cancelled by the caller.",
            [AgentErrorCode.TIMEOUT] = "TIMEOUT: The agent request exceeded a per-call or overall timeout.",
            [AgentErrorCode.TOOL_DISCOVERY_ERROR] = "TOOL_DISCOVERY_ERROR: MCP tools could not be loaded.",
            [AgentErrorCode.INVALID_TOOLSET] = "INVALID_TOOLSET: The MCP server returned an invalid or duplicate toolset.",
            [AgentErrorCode.MODEL_ERROR] = "MODEL_ERROR: The model call failed.",
            [AgentErrorCode.EMPTY_RESPONSE] = "EMPTY_RESPONSE: The model returned an empty response.",
            [AgentErrorCode.TURN_LIMIT_EXCEEDED] = "TURN_LIMIT_EXCEEDED: The maximum model-turn budget was reached.",
            [AgentErrorCode.BUDGET_EXCEEDED] = "BUDGET_EXCEEDED: The maximum tool-call budget was exceeded.",
            [AgentErrorCode.UNKNOWN_TOOL] = "UNKNOWN_TOOL: The model requested an unavailable tool.",
            [AgentErrorCode.INVALID_TOOL_ARGUMENTS] = "INVALID_TOOL_ARGUMENTS: The requested tool arguments were invalid.",
            [AgentErrorCode.REPEAT_CALL_LIMIT] = "REPEAT_CALL_LIMIT: An identical tool call exceeded the repeat limit.",
            [AgentErrorCode.PAGINATION_LIMIT_EXCEEDED] = "PAGINATION_LIMIT_EXCEEDED: The requested page exceeded the maximum page budget.",
            [AgentErrorCode.PAGINATION_SEQUENCE_ERROR] = "PAGINATION_SEQUENCE_ERROR: Pagination parameters violated sequential paging rules.",
            [AgentErrorCode.TOOL_ERROR] = "TOOL_ERROR: A catalogue tool encountered an execution error.",
            [AgentErrorCode.SERIALIZATION_ERROR] = "SERIALIZATION_ERROR: A tool result could not be serialized.",
            [AgentErrorCode.PAYLOAD_LIMIT_EXCEEDED] = "PAYLOAD_LIMIT_EXCEEDED: A tool result exceeded the maximum allowed payload size.",
            [AgentErrorCode.EVIDENCE_INDEXING_ERROR] = "EVIDENCE_INDEXING_ERROR: A tool result did not match the expected evidence schema or paging invariants.",
            [AgentErrorCode.VALIDATION_RETRIES_EXHAUSTED] = "VALIDATION_RETRIES_EXHAUSTED: Output failed schema or provenance validation after maximum retries."
        };

    public static string GetPublicMessage(AgentErrorCode code) =>
        PublicMessages.TryGetValue(code, out var message)
            ? message
            : "MODEL_ERROR: An unexpected orchestration error occurred.";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogueSourceCitation(
    int BookId,
    int? EditionId,
    IReadOnlyList<string> Fields,
    string Claim);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentSourceCitation(
    string DocumentId,
    string ChunkId,
    string Claim);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentEvidenceDraft(
    IReadOnlyList<CatalogueSourceCitation> CatalogueSources,
    IReadOnlyList<DocumentSourceCitation> DocumentSources);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentModelDraft(
    string Status,
    string Answer,
    AgentEvidenceDraft Evidence,
    string? Limitations);

public sealed record ToolExecutionRecord(
    string ToolName,
    int Turn,
    long DurationMs,
    bool Succeeded);
