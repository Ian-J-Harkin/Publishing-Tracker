using PublishingTracker.Api.Models;

namespace PublishingTracker.Api.Extensions;

/// <summary>
/// Fluent IQueryable extension methods for common query filters.
/// Eliminates repeated Where() chains across SalesEndpoints, DashboardEndpoints, and BookService.
/// </summary>
public static class QueryableExtensions
{
    // ── Sale Filters ──────────────────────────────────────────────

    /// <summary>Filters sales to only those belonging to the specified user (via Book.UserId).</summary>
    public static IQueryable<Sale> ForUser(this IQueryable<Sale> query, int userId)
        => query.Where(s => s.Book.UserId == userId);

    /// <summary>Filters sales to a specific book.</summary>
    public static IQueryable<Sale> FilterByBook(this IQueryable<Sale> query, int? bookId)
        => bookId.HasValue ? query.Where(s => s.BookId == bookId.Value) : query;

    /// <summary>Filters sales to a specific platform.</summary>
    public static IQueryable<Sale> FilterByPlatform(this IQueryable<Sale> query, int? platformId)
        => platformId.HasValue ? query.Where(s => s.PlatformId == platformId.Value) : query;

    /// <summary>Filters sales within an inclusive date range. Either bound may be null.</summary>
    public static IQueryable<Sale> InDateRange(this IQueryable<Sale> query, DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue) query = query.Where(s => s.SaleDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(s => s.SaleDate <= endDate.Value);
        return query;
    }

    /// <summary>Filters sales for a specific calendar year.</summary>
    public static IQueryable<Sale> InYear(this IQueryable<Sale> query, int year)
        => query.Where(s => s.SaleDate.Year == year);

    // ── Book Filters ──────────────────────────────────────────────

    /// <summary>Filters books to only those belonging to the specified user.</summary>
    public static IQueryable<Book> ForUser(this IQueryable<Book> query, int userId)
        => query.Where(b => b.UserId == userId);

    /// <summary>Case-insensitive search on book Title or Author.</summary>
    public static IQueryable<Book> Search(this IQueryable<Book> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return query;
        var term = searchTerm.ToLower();
        return query.Where(b => b.Title.ToLower().Contains(term) || b.Author.ToLower().Contains(term));
    }

    // ── Platform Filters ──────────────────────────────────────────

    /// <summary>Case-insensitive search on platform Name.</summary>
    public static IQueryable<Platform> Search(this IQueryable<Platform> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return query;
        var term = searchTerm.ToLower();
        return query.Where(p => p.Name.ToLower().Contains(term));
    }

    // ── Import Job Filters ────────────────────────────────────────

    /// <summary>Filters import jobs to only those belonging to the specified user.</summary>
    public static IQueryable<ImportJob> ForUser(this IQueryable<ImportJob> query, int userId)
        => query.Where(j => j.UserId == userId);
}
