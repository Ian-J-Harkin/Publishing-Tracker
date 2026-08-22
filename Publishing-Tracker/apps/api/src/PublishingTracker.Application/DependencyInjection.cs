using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using MediatR;

namespace PublishingTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        return services;
    }
}
