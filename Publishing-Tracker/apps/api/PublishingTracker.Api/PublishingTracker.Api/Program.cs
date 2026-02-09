using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PublishingTracker.Api.Data;
using PublishingTracker.Api.Features.Auth;
using PublishingTracker.Api.Features.Books;
using PublishingTracker.Api.Features.Dashboard;
using PublishingTracker.Api.Features.Import;
using PublishingTracker.Api.Features.Platforms;
using PublishingTracker.Api.Features.Sales;
using PublishingTracker.Api.Models;
using PublishingTracker.Api.Models.Dtos;
using PublishingTracker.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

if (!builder.Environment.IsEnvironment("Testing"))
{
    if (IsRunningInAzure())
    {
        Console.WriteLine("Running in Azure environment.");
        builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("NeonConnection")));
    }
    else
    {
        Console.WriteLine("Running in Local/Development environment.");
        // Use SQL Server for local development (Requires running SQL Server instance)
        builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), 
                sqlOptions => sqlOptions.EnableRetryOnFailure()));
    }
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
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

if (app.Environment.IsEnvironment("Testing") || app.Environment.IsDevelopment())
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

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
// app.UseAntiforgery();

app.MapAuthEndpoints();
app.MapBookEndpoints();
app.MapSalesEndpoints();
app.MapPlatformEndpoints();
app.MapDashboardEndpoints();
app.MapImportEndpoints();
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
