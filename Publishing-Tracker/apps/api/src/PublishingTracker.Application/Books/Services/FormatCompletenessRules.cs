using PublishingTracker.Domain.Entities;

namespace PublishingTracker.Application.Books.Services;

public sealed record EditionCompletenessEvaluation(
    int EditionId,
    EditionFormat Format,
    IReadOnlyList<string> MissingFields);

public sealed record BookCompletenessEvaluation(
    int BookId,
    string Title,
    IReadOnlyList<string> MissingBookFields,
    IReadOnlyList<EditionCompletenessEvaluation> Editions)
{
    public bool IsIncomplete =>
        MissingBookFields.Count > 0 || Editions.Any(e => e.MissingFields.Count > 0);
}

/// <summary>
/// Authoritative format-specific completeness evaluator for books and editions.
/// Shared by Phase 0 unit tests and Phase 1 missing-metadata catalogue queries.
/// </summary>
public static class FormatCompletenessRules
{
    public static BookCompletenessEvaluation EvaluateBook(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        var missingBookFields = new List<string>();
        if (IsMissing(book.Description)) missingBookFields.Add("Description");
        if (IsMissing(book.Genre)) missingBookFields.Add("Genre");
        if (IsMissing(book.CoverImageUrl)) missingBookFields.Add("CoverImageUrl");
        if (book.Editions.Count == 0) missingBookFields.Add("Editions");
        if (!IsMissing(book.SeriesName) && (book.SeriesNumber is null || book.SeriesNumber <= 0))
        {
            missingBookFields.Add("SeriesNumber");
        }

        var editionEvaluations = book.Editions
            .OrderBy(e => e.Id)
            .Select(EvaluateEdition)
            .ToList();

        return new BookCompletenessEvaluation(book.Id, book.Title, missingBookFields, editionEvaluations);
    }

    public static EditionCompletenessEvaluation EvaluateEdition(Edition edition)
    {
        ArgumentNullException.ThrowIfNull(edition);

        var missing = new List<string>();

        if (edition.PublicationDate is null) missing.Add("PublicationDate");
        if (edition.BasePrice is null) missing.Add("BasePrice");

        switch (edition.Format)
        {
            case EditionFormat.Paperback:
            case EditionFormat.Hardcover:
            case EditionFormat.LargeText:
                if (IsMissing(edition.ISBN)) missing.Add("ISBN");
                if (edition.PageCount is null || edition.PageCount <= 0) missing.Add("PageCount");
                if (IsMissing(edition.Dimensions)) missing.Add("Dimensions");
                break;

            case EditionFormat.Ebook:
                if (!HasIsbnOrPlatformProductId(edition)) missing.Add("ISBN_Or_PlatformProductId");
                if (edition.FileSize is null || edition.FileSize <= 0) missing.Add("FileSize");
                break;

            case EditionFormat.Audiobook:
                if (!HasIsbnOrPlatformProductId(edition)) missing.Add("ISBN_Or_PlatformProductId");
                if (IsMissing(edition.Narrator)) missing.Add("Narrator");
                if (edition.Duration is null || edition.Duration <= TimeSpan.Zero) missing.Add("Duration");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(edition), edition.Format, "Unsupported edition format.");
        }

        return new EditionCompletenessEvaluation(edition.Id, edition.Format, missing);
    }

    private static bool HasIsbnOrPlatformProductId(Edition edition) =>
        !IsMissing(edition.ISBN) ||
        edition.PlatformMetadata.Any(pm => !IsMissing(pm.PlatformProductId));

    private static bool IsMissing(string? value) => string.IsNullOrWhiteSpace(value);
}
