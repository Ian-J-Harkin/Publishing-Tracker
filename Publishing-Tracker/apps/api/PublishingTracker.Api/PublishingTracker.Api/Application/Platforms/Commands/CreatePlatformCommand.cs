using MediatR;
using Microsoft.Extensions.Caching.Memory;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Models;
using PublishingTracker.Domain.Dtos;
using PublishingTracker.Api.Domain.Entities;
using PublishingTracker.Application.Common.Interfaces;

namespace PublishingTracker.Application.Platforms.Commands;

public record CreatePlatformCommand(string Name, string BaseUrl, decimal CommissionRate) : IRequest<Result<PlatformResponseDto>>;

public class CreatePlatformCommandHandler : IRequestHandler<CreatePlatformCommand, Result<PlatformResponseDto>>
{
    private readonly PublishingTrackerDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserService _currentUserService;

    public CreatePlatformCommandHandler(PublishingTrackerDbContext context, IMemoryCache cache, ICurrentUserService currentUserService)
    {
        _context = context;
        _cache = cache;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PlatformResponseDto>> Handle(CreatePlatformCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<PlatformResponseDto>.Failure("Unauthorized");

        var platform = Platform.Create(
            request.Name,
            request.BaseUrl,
            request.CommissionRate,
            createdByUserId: userId
        );

        _context.Platforms.Add(platform);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the cache to ensure the new platform appears in subsequent requests
        _cache.Remove("platforms_all");

        return Result<PlatformResponseDto>.Success(platform.ToResponseDto());
    }
}
