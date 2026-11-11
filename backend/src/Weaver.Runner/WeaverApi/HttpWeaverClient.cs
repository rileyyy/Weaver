using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Weaver.Runner.WeaverApi;

/// <summary>
/// <see cref="IWeaverClient"/> over Weaver's REST API, signed in with an agent key (ADR-0004).
/// <paramref name="http"/> must have Weaver's URL as its base address.
/// </summary>
public sealed class HttpWeaverClient(HttpClient http, string agentApiKey) : IWeaverClient
{
    public const string ActorLabelHeader = "X-Actor-Label";
    private const int PageSize = 200;
    private const string NotDone = "statusCategories=ToDo&statusCategories=Doing";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = http;
    private readonly string _agentApiKey = agentApiKey;

    public async Task<DateOnly> GetTodayAsync(CancellationToken ct) =>
        (await ReadAsync<WeaverToday>("api/calendar/today", ct)).Today;

    public Task<IReadOnlyList<QuickPickRange>> ListQuickPicksAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<QuickPickRange>>("api/quick-picks", ct);

    public async Task<IReadOnlyList<WeaverWorkItem>> ListUnresolvedWhenItemsAsync(CancellationToken ct)
    {
        var items = new List<WeaverWorkItem>();
        string? cursor = null;
        do
        {
            var query = $"api/work-items/search?unresolvedWhenOnly=true&{NotDone}&limit={PageSize}";
            if (cursor is not null)
            {
                query += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var page = await ReadAsync<WeaverWorkItemPage>(query, ct);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return items;
    }

    public async Task<IReadOnlyList<WeaverWorkItem>> SearchOpenItemsAsync(string text, int limit, CancellationToken ct) =>
        (await ReadAsync<WeaverWorkItemPage>(
            $"api/work-items/search?text={Uri.EscapeDataString(text)}&{NotDone}&limit={limit}", ct)).Items;

    public async Task<IReadOnlyList<Guid>> ListLinkedItemIdsAsync(Guid workItemId, CancellationToken ct) =>
        (await ReadAsync<IReadOnlyList<WeaverLink>>($"api/work-items/{workItemId}/links", ct))
            .Select(l => l.LinkedWorkItemId)
            .ToList();

    public Task SetDatesAsync(
        Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, CancellationToken ct)
    {
        // A date left out of the body is unchanged, while null would clear it, so only the dates
        // the answer gave are sent.
        var body = new Dictionary<string, object> { ["expectedVersion"] = expectedVersion };
        if (start is { } s)
        {
            body["startDate"] = s;
        }

        if (end is { } e)
        {
            body["endDate"] = e;
        }

        return WriteAsync(HttpMethod.Patch, $"api/work-items/{workItemId}/schedule", body, actorLabel, ct);
    }

    public Task AddBlockedByLinkAsync(Guid workItemId, Guid blockerId, string actorLabel, CancellationToken ct) =>
        WriteAsync(
            HttpMethod.Post,
            $"api/work-items/{workItemId}/links",
            new { targetWorkItemId = blockerId, type = "BlockedBy" },
            actorLabel,
            ct);

    private async Task<T> ReadAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendAsync(NewRequest(HttpMethod.Get, path), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new WeaverUnavailableException(await DescribeAsync(response, path, ct));
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new WeaverUnavailableException($"GET {path} returned no body");
        }
        catch (JsonException e)
        {
            throw new WeaverUnavailableException($"GET {path} returned JSON the runner can't read", e);
        }
    }

    private async Task WriteAsync(HttpMethod method, string path, object body, string actorLabel, CancellationToken ct)
    {
        var request = NewRequest(method, path, actorLabel);
        request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var description = await DescribeAsync(response, $"{method} {path}", ct);
        throw StopsTheRun(response.StatusCode)
            ? new WeaverUnavailableException(description)
            : new WeaverRejectedException(description);
    }

    /// <summary>
    /// Every request is built here, so a header every call needs (such as a run id) is added in
    /// this one place.
    /// </summary>
    private HttpRequestMessage NewRequest(HttpMethod method, string path, string? actorLabel = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _agentApiKey);
        if (actorLabel is not null)
        {
            request.Headers.Add(ActorLabelHeader, actorLabel);
        }

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

    private static bool StopsTheRun(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || (int)status >= 500;

    private static async Task<string> DescribeAsync(HttpResponseMessage response, string what, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return $"{what} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}";
    }
}
