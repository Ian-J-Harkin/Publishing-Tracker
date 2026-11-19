namespace PublishingTracker.Agent.Contracts;

public sealed record AgentQueryResponse(
    string SchemaVersion,
    string Status,
    string? Answer,
    AgentEvidenceDraft Evidence,
    IReadOnlyList<ToolExecutionRecord> ToolsUsed,
    int Turns,
    int TotalToolCalls,
    string? Limitations,
    string? Error)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string TruncationDisclosureNote =
        "Catalogue results were truncated at the page limit (hasMore=true); additional matching titles exist.";

    public static AgentQueryResponse Answered(
        AgentModelDraft draft,
        bool hasPaginationTruncation,
        IReadOnlyList<ToolExecutionRecord> toolsUsed,
        int turns,
        int totalToolCalls)
    {
        var limitations = draft.Limitations?.Trim();
        if (hasPaginationTruncation &&
            (string.IsNullOrWhiteSpace(limitations) ||
             !limitations.Contains("hasMore=true", StringComparison.OrdinalIgnoreCase)))
        {
            limitations = string.IsNullOrWhiteSpace(limitations)
                ? TruncationDisclosureNote
                : $"{limitations} ({TruncationDisclosureNote})";
        }

        return new AgentQueryResponse(
            SchemaVersion: CurrentSchemaVersion,
            Status: draft.Status,
            Answer: draft.Answer,
            Evidence: draft.Evidence,
            ToolsUsed: toolsUsed,
            Turns: turns,
            TotalToolCalls: totalToolCalls,
            Limitations: string.IsNullOrWhiteSpace(limitations) ? null : limitations,
            Error: null);
    }

    public static AgentQueryResponse Failure(
        AgentErrorCode errorCode,
        IReadOnlyList<ToolExecutionRecord> toolsUsed,
        int turns,
        int totalToolCalls,
        bool hasPaginationTruncation = false) =>
        new(
            SchemaVersion: CurrentSchemaVersion,
            Status: "could_not_answer",
            Answer: null,
            Evidence: new AgentEvidenceDraft([], []),
            ToolsUsed: toolsUsed,
            Turns: turns,
            TotalToolCalls: totalToolCalls,
            Limitations: hasPaginationTruncation ? TruncationDisclosureNote : null,
            Error: AgentErrorCatalog.GetPublicMessage(errorCode));
}
