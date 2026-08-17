using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Application.Platforms.Queries;

public record GetPlatformsQuery(string? Search, bool IncludeInactive = false) : IRequest<Result<List<PlatformResponseDto>>>;

public class GetPlatformsQueryHandler : IRequestHandler<GetPlatformsQuery, Result<List<PlatformResponseDto>>>
{
    private readonly PublishingTrackerDbContext _context;
    private readonly IMemoryCache _cache;

    public GetPlatformsQueryHandler(PublishingTrackerDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Result<List<PlatformResponseDto>>> Handle(GetPlatformsQuery request, CancellationToken cancellationToken)
    {
        // When including inactive platforms (admin view), bypass the cache to always show fresh data.
        if (request.IncludeInactive)
        {
            var allPlatforms = await _context.Platforms
                .Search(request.Search)
                .OrderBy(p => p.Name)
                .Select(p => p.ToResponseDto())
                .ToListAsync(cancellationToken);

            return Result<List<PlatformResponseDto>>.Success(allPlatforms);
        }

        var cacheKey = string.IsNullOrWhiteSpace(request.Search) ? "platforms_all" : $"platforms_search_{request.Search.ToLower()}";

        var platforms = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

            return await _context.Platforms
                .Where(p => p.IsActive)
                .Search(request.Search)
                .OrderBy(p => p.Name)
                .Select(p => p.ToResponseDto())
                .ToListAsync(cancellationToken);
        });

        // GetOrCreateAsync returns nullable if the factory returns null, but ours won't
        return Result<List<PlatformResponseDto>>.Success(platforms!);
    }
}
