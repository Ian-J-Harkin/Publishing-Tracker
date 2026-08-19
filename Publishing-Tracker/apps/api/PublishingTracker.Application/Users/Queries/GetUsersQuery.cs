using MediatR;
using Microsoft.EntityFrameworkCore;
using PublishingTracker.Application.Common.Interfaces;

using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Application.Users.Queries;

public record GetUsersQuery() : IRequest<Result<List<UserResponseDto>>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<List<UserResponseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<UserResponseDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
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
            .ToListAsync(cancellationToken);

        return Result<List<UserResponseDto>>.Success(users);
    }
}
