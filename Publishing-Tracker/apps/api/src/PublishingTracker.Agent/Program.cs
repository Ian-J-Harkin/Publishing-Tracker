using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using PublishingTracker.Agent.Auth;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Orchestration;
using PublishingTracker.Agent.Telemetry;
using PublishingTracker.Agent.Validation;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Infrastructure.Authentication;
using PublishingTracker.Infrastructure.Configuration;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Rag.Persistence;
using PublishingTracker.Rag.Services;
using PublishingTracker.Rag.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddContainerSecrets();

var isDevOrTest = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");

builder.Services.AddOptions<AIProviderOptions>()
    .Bind(builder.Configuration.GetSection(AIProviderOptions.SectionName))
    .Configure(options => Copy(AIProviderSetup.Resolve(options, Environment.GetEnvironmentVariable), options))
    .Validate(options => options.Validate(isDevOrTest) is null && IsMcpEndpointSecure(options, builder.Environment), "AI provider configuration is invalid or the MCP endpoint is not trusted.")
    .ValidateOnStart();

builder.Services.AddOptions<AgentLoopOptions>()
    .Bind(builder.Configuration.GetSection(AgentLoopOptions.SectionName))
    .Validate(options => IsValid(options), "Agent loop configuration is invalid.")
    .ValidateOnStart();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(75);
});

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("JWT configuration is incomplete. Configure Jwt:Key, Jwt:Issuer, and Jwt:Audience using deployment secrets.");
if (AIProviderOptions.ValidateJwtKey(jwtKey) is { } jwtKeyError)
    throw new InvalidOperationException(jwtKeyError);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var userIdRaw = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? principal?.FindFirstValue(JwtRegisteredClaimNames.NameId)
                             ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var hasUserId = int.TryParse(userIdRaw, out var parsedUserId) && parsedUserId > 0;
                var iatRaw = principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);
                var hasValidIat = !string.IsNullOrWhiteSpace(iatRaw) &&
                                  long.TryParse(iatRaw, out var iatSeconds) &&
                                  iatSeconds > 0;
                if (!hasUserId || !hasValidIat)
                {
                    context.Fail("JWT is missing required positive integer user identity or integer iat claim.");
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var connectionString = builder.Configuration.GetConnectionString("NeonConnection")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsEnvironment("Testing") || string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
        options.UseInMemoryDatabase("PublishingTrackerInMemoryDb"));
}
else
{
    builder.Services.AddDbContext<PublishingTrackerDbContext>(options =>
        options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<IRagChunkStore>(sp => sp.GetRequiredService<PublishingTrackerDbContext>());
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(services =>
    AIProviderSetup.CreateEmbeddingGenerator(services.GetRequiredService<IOptions<AIProviderOptions>>().Value));
builder.Services.AddScoped<RagIngestionService>();
builder.Services.AddScoped<RagRetrievalService>();
builder.Services.AddScoped<IRagSearchToolProvider, RagSearchToolFactory>();

builder.Services.AddTransient<PerRequestBearerHandler>();
builder.Services.AddHttpClient<StreamableHttpMcpToolProvider>()
    .AddHttpMessageHandler<PerRequestBearerHandler>();
builder.Services.AddScoped<IMcpToolProvider>(sp => new CompositeToolProvider(
    sp.GetRequiredService<StreamableHttpMcpToolProvider>(),
    sp.GetRequiredService<IRagSearchToolProvider>()));
builder.Services.AddSingleton<IChatClient>(services =>
    AIProviderSetup.CreateChatClient(services.GetRequiredService<IOptions<AIProviderOptions>>().Value));
builder.Services.AddSingleton<AgentOutputValidator>();
builder.Services.AddTransient<AgentLoopRunner>();
builder.Services.AddOpenTelemetry().WithTracing(tracing =>
{
    tracing.AddSource(AgentTelemetry.ActivitySourceName);
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    {
        tracing.AddOtlpExporter();
    }
});

var health = builder.Services.AddHealthChecks();
if (!builder.Environment.IsEnvironment("Testing"))
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        health.AddDbContextCheck<PublishingTrackerDbContext>("database", tags: ["ready"]);
    }

    health.AddCheck<McpApiHealthCheck>("mcp-api", tags: ["ready"]);
}

var app = builder.Build();

if (!builder.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<IRagChunkStore>();
    if (store.IsPostgreSql)
    {
        var db = scope.ServiceProvider.GetRequiredService<PublishingTrackerDbContext>();
        try
        {
            await db.RagDocumentChunks.AsNoTracking().Take(1).ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Database schema is not ready (rag_document_chunks table is missing or unreachable). Ensure PublishingTracker.Api has applied migrations before starting PublishingTracker.Agent.",
                ex);
        }

        if (builder.Configuration.GetValue<bool>("Rag:AutoSeedOnStartup"))
        {
            var corpusDir = Path.Combine(app.Environment.ContentRootPath, "..", "PublishingTracker.Api", "docs", "rag-corpus");
            if (!Directory.Exists(corpusDir))
            {
                corpusDir = Path.Combine(app.Environment.ContentRootPath, "docs", "rag-corpus");
            }
            if (Directory.Exists(corpusDir))
            {
                var ingestion = scope.ServiceProvider.GetRequiredService<RagIngestionService>();
                await ingestion.SeedLockedCorpusAsync(corpusDir);
            }
        }
    }
}
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/agent/query", async (AgentQueryRequest request, AgentLoopRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question must not be empty." });

    var response = await runner.RunVerifiedAsync(request.Question, cancellationToken);
    return Results.Ok(response);
}).RequireAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

static bool IsValid(AgentLoopOptions options)
{
    try
    {
        options.Validate();
        return true;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
}

static bool IsMcpEndpointSecure(AIProviderOptions options, IHostEnvironment environment)
{
    if (!Uri.TryCreate(options.McpEndpoint, UriKind.Absolute, out var endpoint)) return false;
    var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Testing");
    return AIProviderOptions.IsEndpointTrusted(endpoint, isDevOrTest, options.AllowInsecureInternalHttp);
}

static void Copy(AIProviderOptions from, AIProviderOptions to)
{
    to.Type = from.Type;
    to.ModelId = from.ModelId;
    to.Endpoint = from.Endpoint;
    to.ApiKey = from.ApiKey;
    to.OpenRouterModelId = from.OpenRouterModelId;
    to.OpenRouterEndpoint = from.OpenRouterEndpoint;
    to.OpenRouterApiKey = from.OpenRouterApiKey;
    to.GeminiModelId = from.GeminiModelId;
    to.GeminiEndpoint = from.GeminiEndpoint;
    to.GeminiApiKey = from.GeminiApiKey;
    to.OllamaModelId = from.OllamaModelId;
    to.OllamaEndpoint = from.OllamaEndpoint;
    to.McpEndpoint = from.McpEndpoint;
    to.AllowInsecureInternalHttp = from.AllowInsecureInternalHttp;
    to.EmbeddingModelId = from.EmbeddingModelId;
    to.OllamaEmbeddingModelId = from.OllamaEmbeddingModelId;
    to.EmbeddingDimensions = from.EmbeddingDimensions;
}

public partial class Program { }

sealed class McpApiHealthCheck(IHttpClientFactory httpClientFactory, IOptions<AIProviderOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(options.Value.McpEndpoint, UriKind.Absolute, out var mcpUri))
            return HealthCheckResult.Unhealthy("Invalid McpEndpoint URI.");

        var probeUri = new Uri(mcpUri, "/health/live");
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            using var response = await client.GetAsync(probeUri, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("MCP API host is reachable.")
                : HealthCheckResult.Unhealthy($"MCP API host returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("MCP API host is unreachable.", ex);
        }
    }
}
