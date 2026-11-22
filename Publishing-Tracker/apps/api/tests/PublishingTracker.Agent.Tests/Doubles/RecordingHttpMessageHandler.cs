namespace PublishingTracker.Agent.Tests.Doubles;

public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> RequestBodies { get; } = [];

    public RecordingHttpMessageHandler(string? responseBody = null) =>
        _responseBody = responseBody ?? "{\"id\":\"offline\",\"object\":\"chat.completion\",\"created\":0,\"model\":\"test\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
