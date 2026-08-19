using MediatR;
using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Application.Dashboard.Queries;

public record GetDashboardDataQuery(int UserId) : IRequest<DashboardDataDto>;
