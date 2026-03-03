using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;

namespace PublishingTracker.Api.Extensions;

/// <summary>
/// Extension methods for mapping domain entities to their corresponding DTOs.
/// Replaces manual inline mapping code scattered across endpoint handlers and services.
/// </summary>
public static class MappingExtensions
{
    // ── Sale ───────────────────────────────────────────────────────

    /// <summary>
    /// Maps a <see cref="Sale"/> entity (with loaded navigation properties) to a <see cref="SaleDto"/>.
    /// Requires <c>Book</c> and <c>Platform</c> to be loaded/included.
    /// </summary>
    public static SaleDto ToDto(this Sale sale) => new()
    {
        Id = sale.Id,
        BookId = sale.BookId,
        BookTitle = sale.Book?.Title ?? string.Empty,
        PlatformId = sale.PlatformId,
        PlatformName = sale.Platform?.Name ?? string.Empty,
        SaleDate = sale.SaleDate,
        Quantity = sale.Quantity,
        UnitPrice = sale.UnitPrice,
        Royalty = sale.Royalty,
        Revenue = sale.Revenue
    };

    /// <summary>
    /// Creates a <see cref="SaleDto"/> from a <see cref="Sale"/> using explicit title and platform name
    /// (useful when navigation properties aren't loaded).
    /// </summary>
    public static SaleDto ToDto(this Sale sale, string bookTitle, string platformName) => new()
    {
        Id = sale.Id,
        BookId = sale.BookId,
        BookTitle = bookTitle,
        PlatformId = sale.PlatformId,
        PlatformName = platformName,
        SaleDate = sale.SaleDate,
        Quantity = sale.Quantity,
        UnitPrice = sale.UnitPrice,
        Royalty = sale.Royalty,
        Revenue = sale.Revenue
    };

    // ── ImportJob ──────────────────────────────────────────────────

    /// <summary>Maps an <see cref="ImportJob"/> entity to an <see cref="ImportJobDto"/>.</summary>
    public static ImportJobDto ToDto(this ImportJob job) => new()
    {
        Id = job.Id,
        FileName = job.FileName,
        Status = job.Status,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        RecordsProcessed = job.RecordsProcessed,
        RecordsSuccessful = job.RecordsSuccessful,
        RecordsFailed = job.RecordsFailed,
        ErrorLog = job.ErrorLog
    };

    // ── Platform ──────────────────────────────────────────────────

    /// <summary>Maps a <see cref="Platform"/> entity to a <see cref="PlatformResponseDto"/>.</summary>
    public static PlatformResponseDto ToResponseDto(this Platform platform) => new()
    {
        Id = platform.Id,
        Name = platform.Name,
        BaseUrl = platform.BaseUrl ?? string.Empty,
        CommissionRate = platform.CommissionRate,
        IsActive = platform.IsActive
    };
}
