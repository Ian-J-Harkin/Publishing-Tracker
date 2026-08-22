using MediatR;

using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Common.Dtos;

namespace PublishingTracker.Application.Users.Queries;

public record GetProfileQuery() : IRequest<Result<UserResponseDto>>;

public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, Result<UserResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;
    private readonly ICurrentUserService _currentUserService;

    public GetProfileQueryHandler(IApplicationDbContext context, IQueryExecutor queries, ICurrentUserService currentUserService)
    {
        _context = context;
        _queries = queries;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserResponseDto>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Result<UserResponseDto>.Failure("Unauthorized");

        var user = await _queries.FirstOrDefaultAsync(_context.Users.Where(u => u.Id == userId.Value), cancellationToken);
        if (user == null)
            return Result<UserResponseDto>.Failure("User not found");

        var dto = new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        return Result<UserResponseDto>.Success(dto);
    }
}
