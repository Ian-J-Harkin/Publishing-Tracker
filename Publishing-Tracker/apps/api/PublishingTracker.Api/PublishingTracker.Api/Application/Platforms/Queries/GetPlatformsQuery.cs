using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Application.Platforms.Queries;

public record GetPlatformsQuery(string? Search) : IRequest<Result<List<PlatformResponseDto>>>;

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
        var cacheKey = string.IsNullOrWhiteSpace(request.Search) ? "platforms_all" : $"platforms_search_{request.Search.ToLower()}";

        var platforms = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

            return await _context.Platforms
                .Search(request.Search)
                .OrderBy(p => p.Name)
                .Select(p => p.ToResponseDto())
                .ToListAsync(cancellationToken);
        });

        // GetOrCreateAsync returns nullable if the factory returns null, but ours won't
        return Result<List<PlatformResponseDto>>.Success(platforms!);
    }
}
