using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.AI;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AIProviderOptions>()
    .Bind(builder.Configuration.GetSection(AIProviderOptions.SectionName))
    .Configure(options => Copy(AIProviderSetup.Resolve(options, Environment.GetEnvironmentVariable), options))
    .Validate(options => options.Validate() is null && IsMcpEndpointSecure(options, builder.Environment), "AI provider configuration is invalid or the MCP endpoint is not trusted.")
    .ValidateOnStart();

builder.Services.AddOptions<AgentLoopOptions>()
    .Bind(builder.Configuration.GetSection(AgentLoopOptions.SectionName))
    .Validate(options => IsValid(options), "Agent loop configuration is invalid.")
    .ValidateOnStart();

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("JWT configuration is incomplete. Configure Jwt:Key, Jwt:Issuer, and Jwt:Audience using deployment secrets.");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var hasUserId = !string.IsNullOrWhiteSpace(
                    principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    principal?.FindFirstValue(JwtRegisteredClaimNames.NameId) ??
                    principal?.FindFirstValue(JwtRegisteredClaimNames.Sub));
                var iatRaw = principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);
                var hasValidIat = !string.IsNullOrWhiteSpace(iatRaw) &&
                                  long.TryParse(iatRaw, out var iatSeconds) &&
                                  iatSeconds > 0;
                if (!hasUserId || !hasValidIat)
                {
                    context.Fail("JWT is missing required user identity or integer iat claim.");
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<PerRequestBearerHandler>();
builder.Services.AddHttpClient<StreamableHttpMcpToolProvider>()
    .AddHttpMessageHandler<PerRequestBearerHandler>();
builder.Services.AddTransient<IMcpToolProvider>(services => services.GetRequiredService<StreamableHttpMcpToolProvider>());
builder.Services.AddSingleton<IChatClient>(services =>
    AIProviderSetup.CreateChatClient(services.GetRequiredService<IOptions<AIProviderOptions>>().Value));
builder.Services.AddSingleton<AgentOutputValidator>();
builder.Services.AddTransient<AgentLoopRunner>();
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(AgentTelemetry.ActivitySourceName));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/agent/query", async (AgentQueryRequest request, AgentLoopRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question must not be empty." });

    var response = await runner.RunVerifiedAsync(request.Question, cancellationToken);
    return Results.Ok(response);
}).RequireAuthorization();

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
    if (endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return true;
    var loopbackHttp = endpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                       (endpoint.IsLoopback || endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    return loopbackHttp && (environment.IsDevelopment() || environment.IsEnvironment("Testing"));
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
    to.OllamaModelId = from.OllamaModelId;
    to.OllamaEndpoint = from.OllamaEndpoint;
    to.McpEndpoint = from.McpEndpoint;
}

public partial class Program { }
