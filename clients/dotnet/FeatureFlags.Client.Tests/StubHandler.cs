using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FeatureFlags.Client.Tests;

/// <summary>
/// Stands in for the FeatureFlags server. Records what it was asked so a test can assert on the
/// request as well as the answer — the <c>Authorization</c> and <c>If-None-Match</c> headers are
/// half of what this package does.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _answers = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public int CallCount => Requests.Count;

    /// <summary>The last answer queued repeats once the queue runs dry, so a polling test does not
    /// have to enumerate every poll it might make.</summary>
    private Func<HttpRequestMessage, HttpResponseMessage>? _last;

    public StubHandler Answers(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        _answers.Enqueue(answer);

        return this;
    }

    public StubHandler AnswersWithFlags(string environment, object flags, string etag)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { environment, flags });

        return Answers(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            response.Headers.ETag = new EntityTagHeaderValue(etag);

            return response;
        });
    }

    public StubHandler AnswersNotModified(string etag) => Answers(_ =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotModified);
        response.Headers.ETag = new EntityTagHeaderValue(etag);

        return response;
    });

    public StubHandler AnswersWithStatus(HttpStatusCode status) =>
        Answers(_ => new HttpResponseMessage(status));

    public StubHandler Throws() => Answers(_ => throw new HttpRequestException("The stub refused."));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_answers.Count > 0)
        {
            _last = _answers.Dequeue();
        }

        var answer = _last ?? throw new InvalidOperationException("The stub was asked before it was told what to say.");

        return Task.FromResult(answer(request));
    }
}
