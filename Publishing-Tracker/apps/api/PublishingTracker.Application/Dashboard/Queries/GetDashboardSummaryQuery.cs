using MediatR;
using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Application.Dashboard.Queries;

public record GetDashboardSummaryQuery(int UserId) : IRequest<DashboardSummaryDto>;
