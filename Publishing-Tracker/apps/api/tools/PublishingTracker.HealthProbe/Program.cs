using System.Net.Http;

namespace PublishingTracker.HealthProbe;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var targetUrl = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : "http://localhost:8080/health/ready";

        var timeoutSeconds = 5;
        if (args.Length > 1 && int.TryParse(args[1], out var parsedTimeout) && parsedTimeout > 0)
        {
            timeoutSeconds = parsedTimeout;
        }

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            await Console.Error.WriteLineAsync($"[HealthProbe] Error: Invalid target URL '{targetUrl}'");
            return 1;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var client = new HttpClient();

        try
        {
            var response = await client.GetAsync(uri, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[HealthProbe] Healthy: {uri} returned {(int)response.StatusCode} {response.ReasonPhrase}");
                return 0;
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            await Console.Error.WriteLineAsync($"[HealthProbe] Unhealthy: {uri} returned {(int)response.StatusCode} {response.ReasonPhrase}. Response: {content}");
            return 1;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync($"[HealthProbe] Timeout: {uri} did not respond within {timeoutSeconds}s.");
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[HealthProbe] Error connecting to {uri}: {ex.Message}");
            return 1;
        }
    }
}
