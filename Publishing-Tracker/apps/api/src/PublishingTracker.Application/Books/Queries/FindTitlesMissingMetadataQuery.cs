using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Books.Services;

namespace PublishingTracker.Application.Books.Queries;

public record FindTitlesMissingMetadataQuery(
    string? Format = null,
    string? MissingField = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<MissingMetadataPageDto>>;

public sealed record MissingEditionFieldsDto(
    int EditionId,
    string Format,
    IReadOnlyList<string> MissingFields);

public sealed record MissingTitleItemDto(
    int BookId,
    string Title,
    IReadOnlyList<string> MissingBookFields,
    IReadOnlyList<MissingEditionFieldsDto> Editions);

public sealed record MissingMetadataPageDto(
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore,
    IReadOnlyList<MissingTitleItemDto> Items);

public sealed class FindTitlesMissingMetadataQueryHandler
    : IRequestHandler<FindTitlesMissingMetadataQuery, Result<MissingMetadataPageDto>>
{
    private const int MaxPageSize = 50;

    private static readonly string[] KnownFields =
    [
        "Description", "Genre", "CoverImageUrl", "Editions", "SeriesNumber",
        "PublicationDate", "BasePrice", "ISBN", "PageCount", "Dimensions",
        "ISBN_Or_PlatformProductId", "FileSize", "Narrator", "Duration"
    ];

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public FindTitlesMissingMetadataQueryHandler(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MissingMetadataPageDto>> Handle(
        FindTitlesMissingMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return Result<MissingMetadataPageDto>.Failure("UNAUTHORIZED: Authenticated user required.");

        if (request.Page < 1)
            return Result<MissingMetadataPageDto>.Failure("INVALID_ARGUMENT: Page must be greater than or equal to 1.");

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
            return Result<MissingMetadataPageDto>.Failure($"INVALID_ARGUMENT: PageSize must be between 1 and {MaxPageSize}.");

        EditionFormat? format = null;
        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            // Match declared names only: Enum.TryParse would also accept numbers and comma lists.
            var name = Enum.GetNames<EditionFormat>()
                .FirstOrDefault(n => n.Equals(request.Format.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name is null)
            {
                return Result<MissingMetadataPageDto>.Failure(
                    $"INVALID_ARGUMENT: Unsupported format '{request.Format}'. Allowed values: Ebook, Paperback, Hardcover, Audiobook, LargeText.");
            }

            format = Enum.Parse<EditionFormat>(name);
        }

        string? missingField = null;
        if (!string.IsNullOrWhiteSpace(request.MissingField))
        {
            missingField = KnownFields.FirstOrDefault(f => f.Equals(request.MissingField.Trim(), StringComparison.OrdinalIgnoreCase));
            if (missingField is null)
            {
                return Result<MissingMetadataPageDto>.Failure(
                    $"INVALID_ARGUMENT: Unsupported missingField '{request.MissingField}'. Allowed values: {string.Join(", ", KnownFields)}.");
            }
        }

        var books = await _db.Books
            .AsNoTracking()
            .Where(b => b.UserId == userId.Value)
            .Include(b => b.Editions)
                .ThenInclude(e => e.PlatformMetadata)
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

        var matches = new List<MissingTitleItemDto>();
        foreach (var book in books)
        {
            var item = Project(FormatCompletenessRules.EvaluateBook(book), format, missingField);
            if (item is not null) matches.Add(item);
        }

        var totalCount = matches.Count;
        var skip = (long)(request.Page - 1) * request.PageSize;
        var items = skip >= totalCount
            ? []
            : matches.Skip((int)skip).Take(request.PageSize).ToList();

        return Result<MissingMetadataPageDto>.Success(new MissingMetadataPageDto(
            request.Page,
            request.PageSize,
            totalCount,
            HasMore: skip + request.PageSize < totalCount,
            items));
    }

    private static MissingTitleItemDto? Project(
        BookCompletenessEvaluation eval,
        EditionFormat? format,
        string? missingField)
    {
        var editions = eval.Editions.Where(e => e.MissingFields.Count > 0);
        if (format is not null)
            editions = editions.Where(e => e.Format == format.Value);

        if (missingField is not null)
            editions = editions.Where(e => e.MissingFields.Contains(missingField, StringComparer.OrdinalIgnoreCase));

        var matchingEditions = editions
            .Select(e => new MissingEditionFieldsDto(e.EditionId, e.Format.ToString(), e.MissingFields))
            .ToList();

        // A format filter targets edition gaps only, so book-level gaps neither qualify nor appear.
        IReadOnlyList<string> bookFields = format is not null ? [] : eval.MissingBookFields;

        var include = matchingEditions.Count > 0
            || (format is null
                && bookFields.Count > 0
                && (missingField is null || bookFields.Contains(missingField, StringComparer.OrdinalIgnoreCase)));

        return include
            ? new MissingTitleItemDto(eval.BookId, eval.Title, bookFields, matchingEditions)
            : null;
    }
}
