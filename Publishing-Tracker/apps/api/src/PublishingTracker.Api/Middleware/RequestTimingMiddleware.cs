using System.Diagnostics;

namespace PublishingTracker.Api.Middleware;

/// <summary>
/// Middleware that logs the elapsed time for every HTTP request.
/// Part of the monitoring/observability layer.
/// NOTE: Planned migration to Application Insights for production telemetry.
/// </summary>
public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        // Allow the request to proceed
        await _next(context);

        sw.Stop();

        _logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds);
    }
}

/// <summary>
/// Extension method to register the middleware in the pipeline.
/// </summary>
public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app)
        => app.UseMiddleware<RequestTimingMiddleware>();
}
