using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Extensions;
using PublishingTracker.Api.Services;
using System.Text;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureServices(builder);

        var app = builder.Build();

        ConfigureMiddleware(app);

        app.Run();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.WithOrigins(allowedOrigins!)
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        if (IsRunningInAzure())
        {
            Console.WriteLine("Running in Azure environment.");
            builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("NeonConnection")));
        }
        else
        {
            Console.WriteLine("Running in Local/Development environment.");
            builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
                };
                var jwtIssuer = builder.Configuration["Jwt:Issuer"];
                var jwtAudience = builder.Configuration["Jwt:Audience"];
                var jwtKey = builder.Configuration["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
                {
                    throw new InvalidOperationException("JWT configuration is incomplete.");
                }
            });

        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        if (!app.Environment.IsEnvironment("Testing"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            Console.WriteLine("Creating database scope");
            try
            {
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
                    try
                    {
                        db.Database.Migrate();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Migration failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database setup failed: {ex.Message}");
            }
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // Register routes
        app.MapAuthRoutes();
        app.MapBookRoutes();
        app.MapSalesRoutes();
        app.MapPlatformRoutes();
        app.MapDashboardRoutes();
        app.MapImportRoutes();

        // fix for azure port
        app.Urls.Add("http://*:8080");
    }

    private static bool IsRunningInAzure()
    {
        string? instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        string? hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

        return !string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(hostname);
    }
}
