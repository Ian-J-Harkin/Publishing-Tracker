using MediatR;
using PublishingTracker.Application.Common.Dtos;

namespace PublishingTracker.Application.Dashboard.Queries;

public record GetDashboardDataQuery(int UserId) : IRequest<DashboardDataDto>;
