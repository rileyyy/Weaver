using System.Net;
using System.Text;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>Answers every request with <see cref="Respond"/> and records each one with its body.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json("{}");

    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return Respond(request);
    }
}
