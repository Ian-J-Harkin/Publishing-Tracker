using MediatR;
using PublishingTracker.Application.Common.Interfaces;

using PublishingTracker.Application.Common.Dtos;

namespace PublishingTracker.Application.Users.Queries;

public record GetUsersQuery() : IRequest<Result<List<UserResponseDto>>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<List<UserResponseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IQueryExecutor _queries;

    public GetUsersQueryHandler(IApplicationDbContext context, IQueryExecutor queries)
    {
        _context = context;
        _queries = queries;
    }

    public async Task<Result<List<UserResponseDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _queries.ToListAsync(_context.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                CreatedAt = u.CreatedAt
            })
            , cancellationToken);

        return Result<List<UserResponseDto>>.Success(users);
    }
}
