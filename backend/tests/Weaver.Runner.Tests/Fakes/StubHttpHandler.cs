using System.Net;
using System.Text;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>
/// Answers every request with <see cref="Respond"/> and records each one with its body, and the
/// Content-Length it was sent with: read after the body, a buffered length would always be set.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json("{}");

    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public List<long?> SentContentLengths { get; } = [];

    public bool Disposed { get; private set; }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentContentLengths.Add(request.Content?.Headers.ContentLength);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return Respond(request);
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
