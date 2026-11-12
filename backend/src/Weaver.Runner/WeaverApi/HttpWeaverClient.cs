namespace Weaver.Runner.WeaverApi;

/// <summary>
/// <see cref="IWeaverClient"/> over Weaver's REST API, signed in with an agent key (ADR-0004).
/// <paramref name="http"/> must have Weaver's URL as its base address. Every write is tagged with
/// <paramref name="runId"/>, a fresh one per client unless given, so one client is one run.
/// </summary>
public sealed class HttpWeaverClient(HttpClient http, string agentApiKey, string? runId = null) : IWeaverClient
{
    private const int PageSize = 200;
    private const string NotDone = "statusCategories=ToDo&statusCategories=Doing";

    private readonly WeaverRest _rest = new(http, agentApiKey, runId ?? Guid.NewGuid().ToString());

    public async Task<DateOnly> GetTodayAsync(CancellationToken ct) =>
        (await _rest.ReadRunWideAsync<WeaverToday>("api/calendar/today", ct)).Today;

    public Task<IReadOnlyList<QuickPickRange>> ListQuickPicksAsync(CancellationToken ct) =>
        _rest.ReadRunWideAsync<IReadOnlyList<QuickPickRange>>("api/quick-picks", ct);

    public async Task<IReadOnlyList<WeaverWorkItem>> ListUnresolvedWhenItemsAsync(bool includeEdited, CancellationToken ct)
    {
        var items = new List<WeaverWorkItem>();
        var filter = includeEdited ? "unresolvedWhenOnly=true&includeEdited=true" : "unresolvedWhenOnly=true";
        string? cursor = null;
        do
        {
            var query = $"api/work-items/search?{filter}&{NotDone}&limit={PageSize}";
            if (cursor is not null)
            {
                query += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var page = await _rest.ReadRunWideAsync<WeaverWorkItemPage>(query, ct);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return items;
    }

    public async Task<IReadOnlyList<WeaverWorkItem>> SearchOpenItemsAsync(string text, int limit, CancellationToken ct) =>
        (await _rest.ReadAsync<WeaverWorkItemPage>(
            $"api/work-items/search?text={Uri.EscapeDataString(text)}&{NotDone}&limit={limit}", ct)).Items;

    public async Task<IReadOnlyList<Guid>> ListLinkedItemIdsAsync(Guid workItemId, CancellationToken ct) =>
        (await _rest.ReadAsync<IReadOnlyList<WeaverLink>>($"api/work-items/{workItemId}/links", ct))
            .Select(l => l.LinkedWorkItemId)
            .ToList();

    public Task SetDatesAsync(
        Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, CancellationToken ct) =>
        _rest.SetDatesAsync(workItemId, start, end, expectedVersion, actorLabel, ct);

    public Task ReplaceDatesAsync(
        Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, CancellationToken ct) =>
        _rest.WriteAsync(
            HttpMethod.Patch,
            $"api/work-items/{workItemId}/schedule",
            new { startDate = start, endDate = end, expectedVersion },
            actorLabel,
            ct);

    public Task AddBlockedByLinkAsync(Guid workItemId, Guid blockerId, string actorLabel, CancellationToken ct) =>
        _rest.WriteAsync(
            HttpMethod.Post,
            $"api/work-items/{workItemId}/links",
            new { targetWorkItemId = blockerId, type = "BlockedBy" },
            actorLabel,
            ct);
}
