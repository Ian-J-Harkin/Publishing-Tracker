using System.ComponentModel;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PublishingTracker.Application.Books.Queries;

namespace PublishingTracker.Mcp.Tools;

[McpServerToolType]
public sealed class CatalogueMcpTools(ISender mediator)
{
    [McpServerTool(
        Name = "find_titles_missing_metadata",
        UseStructuredContent = true,
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("Find books and editions owned by the authenticated caller that are missing required metadata fields. Returns stable bookId and editionId values, missing field names, and bounded pagination metadata.")]
    public async Task<MissingMetadataPageDto> FindTitlesMissingMetadataAsync(
        [Description("Optional edition format filter: Ebook, Paperback, Hardcover, Audiobook, or LargeText.")]
        string? format = null,
        [Description("Optional missing field name filter (e.g. Description, Genre, CoverImageUrl, Editions, SeriesNumber, ISBN, PageCount, Dimensions, ISBN_Or_PlatformProductId, FileSize, Narrator, Duration, PublicationDate, BasePrice).")]
        string? missingField = null,
        [Description("1-based page number (default 1).")]
        int page = 1,
        [Description("Items per page from 1 to 50 (default 20).")]
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new FindTitlesMissingMetadataQuery(format, missingField, page, pageSize),
            cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? result.Value
            : throw new McpException(result.ErrorMessage ?? "Failed to query missing metadata.");
    }

    [McpServerTool(
        Name = "get_title_details",
        UseStructuredContent = true,
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("Get full book metadata, all editions, format completeness flags, and platform publication summaries for a single bookId owned by the authenticated caller.")]
    public async Task<TitleDetailsDto> GetTitleDetailsAsync(
        [Description("Positive integer Book ID owned by the caller.")]
        int bookId,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTitleDetailsQuery(bookId), cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? result.Value
            : throw new McpException(result.ErrorMessage ?? "Failed to load title details.");
    }
}
