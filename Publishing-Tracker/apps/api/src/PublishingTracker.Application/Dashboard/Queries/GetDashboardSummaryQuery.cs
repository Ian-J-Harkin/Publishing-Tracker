using MediatR;
using PublishingTracker.Application.Common.Dtos;

namespace PublishingTracker.Application.Dashboard.Queries;

public record GetDashboardSummaryQuery(int UserId) : IRequest<DashboardSummaryDto>;
