using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weaver.Runner.Model;

/// <summary>
/// A model behind any OpenAI-compatible <c>chat/completions</c> endpoint (Ollama, LM Studio,
/// vLLM, OpenAI, ...), so swapping models is a config change (ADR-0010). <paramref name="http"/>
/// must have the endpoint's base (e.g. <c>http://ollama:11434/v1/</c>) as its base address.
/// </summary>
/// <param name="jsonMode">
/// Asks for <c>response_format: json_object</c>, which makes small models far more reliable.
/// Some servers reject it, so it can be turned off; the answer is checked either way.
/// </param>
public sealed class OpenAiCompatibleModelClient(HttpClient http, string model, string? apiKey, bool jsonMode) : IModelClient
{
    private readonly HttpClient _http = http;
    private readonly string _model = model;
    private readonly string? _apiKey = apiKey;
    private readonly bool _jsonMode = jsonMode;

    public async Task<string> CompleteAsync(ModelPrompt prompt, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(RequestBody(prompt)),
        };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ModelUnavailableException(
                $"The model endpoint {_http.BaseAddress} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        return ReplyText(await response.Content.ReadAsStringAsync(ct));
    }

    private JsonObject RequestBody(ModelPrompt prompt)
    {
        var body = new JsonObject
        {
            ["model"] = _model,
            ["temperature"] = 0,
            ["stream"] = false,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = prompt.System },
                new JsonObject { ["role"] = "user", ["content"] = prompt.User }),
        };
        if (_jsonMode)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }

        return body;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException e)
        {
            throw new ModelUnavailableException($"The model endpoint {_http.BaseAddress} can't be reached: {e.Message}", e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ModelUnavailableException($"The model endpoint {_http.BaseAddress} timed out", e);
        }
    }

    /// <summary>A body without a reply is the model's problem with this one item, not the endpoint's.</summary>
    private static string ReplyText(string responseBody)
    {
        try
        {
            return JsonNode.Parse(responseBody)?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return "";
        }
    }
}
