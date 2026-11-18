using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Books.Services;

namespace PublishingTracker.Application.Books.Queries;

public record GetTitleDetailsQuery(int BookId) : IRequest<Result<TitleDetailsDto>>;

public sealed record PlatformSummaryDto(
    int PlatformId,
    string PlatformName,
    bool IsPublished,
    DateTime? PublishedDate,
    string? PlatformProductId);

public sealed record EditionDetailsDto(
    int EditionId,
    string Format,
    string? ISBN,
    DateTime? PublicationDate,
    decimal? BasePrice,
    int? PageCount,
    string? Dimensions,
    decimal? FileSize,
    string? Narrator,
    TimeSpan? Duration,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<PlatformSummaryDto> PlatformSummaries);

public sealed record TitleDetailsDto(
    int BookId,
    string Title,
    string Author,
    string? SeriesName,
    int? SeriesNumber,
    string? Genre,
    string? Description,
    string? CoverImageUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string> MissingBookFields,
    IReadOnlyList<EditionDetailsDto> Editions);

public sealed class GetTitleDetailsQueryHandler : IRequestHandler<GetTitleDetailsQuery, Result<TitleDetailsDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetTitleDetailsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TitleDetailsDto>> Handle(GetTitleDetailsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not int userId)
            return Result<TitleDetailsDto>.Failure("UNAUTHORIZED: Authenticated user required.");

        if (request.BookId <= 0)
            return Result<TitleDetailsDto>.Failure("INVALID_ARGUMENT: bookId must be a positive integer.");

        var book = await _db.Books
            .AsNoTracking()
            .Where(b => b.Id == request.BookId && b.UserId == userId)
            .Include(b => b.Editions)
                .ThenInclude(e => e.PlatformMetadata)
                    .ThenInclude(pm => pm.Platform)
            .FirstOrDefaultAsync(cancellationToken);

        // Same response for absent and non-owned titles so IDs cannot be probed across tenants.
        if (book is null)
            return Result<TitleDetailsDto>.Failure("NOT_FOUND_OR_UNAUTHORIZED: Title was not found or is not accessible.");

        var evaluation = FormatCompletenessRules.EvaluateBook(book);
        var missingByEdition = evaluation.Editions.ToDictionary(e => e.EditionId, e => e.MissingFields);

        var editions = book.Editions
            .OrderBy(e => e.Id)
            .Select(e => new EditionDetailsDto(
                e.Id,
                e.Format.ToString(),
                e.ISBN,
                e.PublicationDate,
                e.BasePrice,
                e.PageCount,
                e.Dimensions,
                e.FileSize,
                e.Narrator,
                e.Duration,
                missingByEdition[e.Id],
                e.PlatformMetadata
                    .OrderBy(pm => pm.Id)
                    .Select(pm => new PlatformSummaryDto(
                        pm.PlatformId,
                        pm.Platform.Name,
                        pm.IsPublished,
                        pm.PublishedDate,
                        pm.PlatformProductId))
                    .ToList()))
            .ToList();

        return Result<TitleDetailsDto>.Success(new TitleDetailsDto(
            book.Id,
            book.Title,
            book.Author,
            book.SeriesName,
            book.SeriesNumber,
            book.Genre,
            book.Description,
            book.CoverImageUrl,
            book.CreatedAt,
            book.UpdatedAt,
            evaluation.MissingBookFields,
            editions));
    }
}
