using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using PublishingTracker.Rag.Contracts;
using PublishingTracker.Rag.Services;

namespace PublishingTracker.Rag.Tools;

public interface IRagSearchToolProvider
{
    int ResolveAuthenticatedUserId();
    IReadOnlyList<AIFunction> CreateToolsForCaller(int authenticatedUserId);
}

public sealed class RagSearchToolFactory(
    RagRetrievalService retrievalService,
    IHttpContextAccessor httpContextAccessor) : IRagSearchToolProvider
{
    public const string ToolName = "search_publishing_guidelines";

    public int ResolveAuthenticatedUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authenticated caller is required.");

        var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? principal.FindFirstValue("nameid")
                 ?? principal.FindFirstValue("sub");

        if (!int.TryParse(rawId, out var userId) || userId <= 0)
            throw new UnauthorizedAccessException("Authenticated caller token lacks a valid positive integer user ID.");

        return userId;
    }

    public IReadOnlyList<AIFunction> CreateToolsForCaller(int authenticatedUserId)
    {
        if (authenticatedUserId <= 0)
            throw new UnauthorizedAccessException("Authenticated positive userId is required.");

        // Model-visible schema exposes ONLY (query, topK); authenticatedUserId is host-bound by closure.
        RagSearchDelegate search =
            [Description("Search shared publishing guidelines and your private metadata notes. Returns documentId, chunkId, title, excerpt, and cosineDistance.")]
            (
                [Description("Natural-language search query (1 to 500 characters).")] string query,
                [Description("Maximum results to return from 1 to 8 (default 5).")] int topK = 5,
                CancellationToken cancellationToken = default) =>
                retrievalService.SearchAsync(authenticatedUserId, query, topK, cancellationToken);

        return
        [
            AIFunctionFactory.Create(search, new AIFunctionFactoryOptions
            {
                Name = ToolName,
                Description = "Search shared publishing guidelines and your private metadata notes. Returns documentId, chunkId, title, excerpt, and cosineDistance.",
                MarshalResult = static (result, _, _) => ValueTask.FromResult(result)
            })
        ];
    }

    private delegate Task<RagSearchResultDto> RagSearchDelegate(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
