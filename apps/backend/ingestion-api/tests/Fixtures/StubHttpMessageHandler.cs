using System.Net;

namespace IngestionApi.Tests.Fixtures;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<Uri> Requests { get; } = [];

    public List<string> AccessTokens { get; } = [];

    public StubHttpMessageHandler EnqueueJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        return this;
    }

    public StubHttpMessageHandler EnqueueStatus(HttpStatusCode status)
    {
        _responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(string.Empty) });
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null)
        {
            Requests.Add(request.RequestUri);
        }

        if (request.Headers.TryGetValues("Access-Token", out var tokens))
        {
            AccessTokens.AddRange(tokens);
        }

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No stub response was queued for this request.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
