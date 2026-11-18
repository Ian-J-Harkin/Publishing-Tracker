using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.Server;
using PublishingTracker.Mcp.Tools;
using Microsoft.IdentityModel.Tokens;
using PublishingTracker.Application.Interfaces;
using PublishingTracker.Api.Endpoints;
using PublishingTracker.Application;
using PublishingTracker.Infrastructure;
using PublishingTracker.Api.Middleware;
using System.Text;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(allowedOrigins!)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = IsRunningInAzure()
        ? builder.Configuration.GetConnectionString("NeonConnection")
        : builder.Configuration.GetConnectionString("DefaultConnection");

    if (IsRunningInAzure())
    {
        Console.WriteLine("Running in Azure environment.");
    }
    else
    {
        Console.WriteLine("Running in Local/Development environment.");
    }

    builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
        options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.EnableRetryOnFailure();
                npgsql.MigrationsAssembly(typeof(PublishingTrackerDbContext).Assembly.GetName().Name!);
            }));
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"];
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];

        if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience)) {
            throw new InvalidOperationException("JWT configuration is incomplete. Please check appsettings.json or environment variables.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        
    });


builder.Services.AddAuthorization();
// Stateless: each POST /mcp runs inside its own authenticated HttpContext, so tools resolve the caller per request.
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<CatalogueMcpTools>();

builder.Services.AddMemoryCache();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// ── Health Checks ─────────────────────────────────────────────────
// NOTE: Planned migration to Application Insights for production telemetry.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PublishingTrackerDbContext>("database")
    .AddCheck("temp-storage", () =>
    {
        var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
        return Directory.Exists(tempDir)
            ? HealthCheckResult.Healthy("temp_uploads directory exists.")
            : HealthCheckResult.Degraded("temp_uploads directory is missing.");
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Currency Settings
builder.Services.Configure<CurrencySettings>(
    builder.Configuration.GetSection(CurrencySettings.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var errorFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (errorFeature != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(errorFeature.Error, "Unhandled exception occurred.");

            if (app.Environment.IsDevelopment())
            {
                 // In Development, return detailed error info
                 await context.Response.WriteAsJsonAsync(new 
                 { 
                     error = "Internal Server Error", 
                     message = errorFeature.Error.Message, 
                     stackTrace = errorFeature.Error.StackTrace 
                 });
            }
            else
            {
                 // In Production, return generic message for security
                 await context.Response.WriteAsJsonAsync(new { error = "An error occurred. Please contact support." });
            }
        }
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    Console.WriteLine($"Creating database scope");
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
            // wrap in try-catch to handle potential migration issues
            try
            {
                if (db.Database.IsRelational())
                {
                    db.Database.Migrate();
                }
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

// ── Request Timing Middleware (monitoring) ─────────────────────────
app.UseRequestTiming();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.UseRoleValidation();

app.MapAuthEndpoints();
app.MapBookEndpoints();
app.MapSalesEndpoints();
app.MapPlatformEndpoints();
app.MapDashboardEndpoints();
app.MapImportEndpoints();
app.MapUserEndpoints();
app.MapMcp("/mcp").RequireAuthorization();

// ── Health Check Endpoint ─────────────────────────────────────────
app.MapHealthChecks("/health");

// fix for azure port
//app.Urls.Add("http://*:8080");
app.Run();

static bool IsRunningInAzure()
{
    string? instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
    string? hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

    return !string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(hostname);
}


public partial class Program { }
