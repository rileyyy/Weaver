using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Weaver.Runner.WeaverApi;

/// <summary>
/// The HTTP side every Weaver client in the runner shares: signing requests with the agent key,
/// tagging writes with the actor label and the run id (ticket 50), and sorting failures into
/// <see cref="WeaverUnavailableException"/> (stop the run) and <see cref="WeaverRejectedException"/>
/// (skip the one item). <paramref name="http"/> must have Weaver's URL as its base address.
/// </summary>
internal sealed class WeaverRest(HttpClient http, string agentApiKey, string runId)
{
    public const string ActorLabelHeader = "X-Actor-Label";
    public const string RunIdHeader = "X-Agent-Run";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly string _agentApiKey = agentApiKey;
    private readonly string _runId = runId;

    /// <summary>A read the whole run needs, so any failure of it stops the run.</summary>
    public async Task<T> ReadRunWideAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            return await ReadAsync<T>(path, ct);
        }
        catch (WeaverRejectedException e)
        {
            throw new WeaverUnavailableException(e.Message, e);
        }
    }

    public async Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendAsync(NewRequest(HttpMethod.Get, path), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await FailureAsync(response, $"GET {path}", ct);
        }

        return await ReadBodyAsync<T>(response, $"GET {path}", ct);
    }

    public async Task WriteAsync(HttpMethod method, string path, object body, string actorLabel, CancellationToken ct)
    {
        using var response = await SendWriteAsync(method, path, body, actorLabel, ct);
    }

    /// <summary>A write whose response body the caller needs, such as the item it created.</summary>
    public async Task<T> WriteAsync<T>(HttpMethod method, string path, object body, string actorLabel, CancellationToken ct)
    {
        using var response = await SendWriteAsync(method, path, body, actorLabel, ct);
        return await ReadBodyAsync<T>(response, $"{method} {path}", ct);
    }

    private async Task<HttpResponseMessage> SendWriteAsync(
        HttpMethod method, string path, object body, string actorLabel, CancellationToken ct)
    {
        var request = NewRequest(method, path);
        request.Headers.Add(ActorLabelHeader, actorLabel);
        request.Headers.Add(RunIdHeader, _runId);
        // Buffered rather than JsonContent's chunked stream: some proxies refuse a request body
        // without a Content-Length.
        request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        var response = await SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw await FailureAsync(response, $"{method} {path}", ct);
        }
    }

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, string what, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new WeaverUnavailableException($"{what} returned no body");
        }
        catch (JsonException e)
        {
            throw new WeaverUnavailableException($"{what} returned JSON the runner can't read", e);
        }
    }

    /// <summary>
    /// The key goes on each request rather than on the client's default headers because the
    /// <see cref="HttpClient"/> is handed in, so this class doesn't own its settings.
    /// </summary>
    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _agentApiKey);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            try
            {
                return await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException e)
            {
                throw new WeaverUnavailableException($"Weaver can't be reached at {_http.BaseAddress}: {e.Message}", e);
            }
            catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
            {
                throw new WeaverUnavailableException($"Weaver at {_http.BaseAddress} timed out", e);
            }
        }
    }

    /// <summary>
    /// A refused key or a server error will fail every later request too, so it stops the run;
    /// anything else (400, 404, 409) is about the one item the request was for.
    /// </summary>
    private static async Task<Exception> FailureAsync(HttpResponseMessage response, string what, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var description = $"{what} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}";
        return StopsTheRun(response.StatusCode)
            ? new WeaverUnavailableException(description)
            : new WeaverRejectedException(description);
    }

    private static bool StopsTheRun(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || (int)status >= 500;
}
