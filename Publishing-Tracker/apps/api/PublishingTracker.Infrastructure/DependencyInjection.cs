using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Channels;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Application.Import.Models;
using PublishingTracker.Infrastructure.Authentication;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Infrastructure.Persistence.Repositories;
using PublishingTracker.Infrastructure.Services;
using PublishingTracker.Application.Common.Dtos;
using Microsoft.EntityFrameworkCore;

namespace PublishingTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<PublishingTrackerDbContext>());
        services.AddScoped<IQueryExecutor, QueryExecutor>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICsvReader, CsvReaderService>();
        services.AddScoped<ICurrencyConverterService, StaticCurrencyConverterService>();
        services.AddTransient<IEmailService, EmailService>();

        services.AddSingleton(Channel.CreateBounded<ImportRequest>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        }));
        services.AddSingleton<ImportBackgroundService>();
        services.AddSingleton<IImportQueue>(sp => sp.GetRequiredService<ImportBackgroundService>());
        services.AddHostedService(sp => sp.GetRequiredService<ImportBackgroundService>());

        return services;
    }
}
