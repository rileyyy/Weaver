namespace Weaver.Runner.WeaverApi;

/// <summary>
/// <see cref="IMentionsWeaverClient"/> over Weaver's REST API, signed in with the mentions task
/// kind's agent key.
/// </summary>
public sealed class HttpMentionsWeaverClient(HttpClient http, string agentApiKey) : IMentionsWeaverClient
{
    private readonly WeaverRest _rest = new(http, agentApiKey, runId: null);

    public async Task<DateOnly> GetTodayAsync(CancellationToken ct) =>
        (await _rest.ReadRunWideAsync<WeaverToday>("api/calendar/today", ct)).Today;

    public Task<IReadOnlyList<WeaverPendingMention>> ListPendingMentionsAsync(CancellationToken ct) =>
        _rest.ReadRunWideAsync<IReadOnlyList<WeaverPendingMention>>("api/mentions/pending", ct);

    public Task<WeaverItemDetail> GetItemAsync(Guid workItemId, CancellationToken ct) =>
        _rest.ReadAsync<WeaverItemDetail>($"api/work-items/{workItemId}", ct);

    public Task<IReadOnlyList<WeaverComment>> ListCommentsAsync(Guid workItemId, CancellationToken ct) =>
        _rest.ReadAsync<IReadOnlyList<WeaverComment>>($"api/work-items/{workItemId}/comments", ct);

    public Task<IReadOnlyList<WeaverHistoryEntry>> ListHistoryAsync(Guid workItemId, CancellationToken ct) =>
        _rest.ReadAsync<IReadOnlyList<WeaverHistoryEntry>>($"api/work-items/{workItemId}/history", ct);

    public Task<IReadOnlyList<WeaverSubItem>> ListSubItemsAsync(Guid parentId, CancellationToken ct) =>
        _rest.ReadAsync<IReadOnlyList<WeaverSubItem>>($"api/work-items?parentId={parentId}", ct);

    /// <summary>Statuses come back in their board order, so the first To Do one is where new work starts.</summary>
    public async Task<Guid> GetFirstToDoStatusIdAsync(Guid spaceId, CancellationToken ct)
    {
        var statuses = await _rest.ReadAsync<IReadOnlyList<WeaverStatus>>($"api/statuses?spaceId={spaceId}", ct);
        return statuses.OrderBy(s => s.Order).FirstOrDefault(s => s.Category == "ToDo")?.Id
            ?? throw new WeaverRejectedException($"Space {spaceId} has no To Do status");
    }

    public async Task<int> AddSubItemAsync(
        Guid parentId, string title, Guid statusId, string actorLabel, string runId, CancellationToken ct) =>
        (await _rest.WithRunId(runId).WriteAsync<WeaverCreatedItem>(
            HttpMethod.Post,
            "api/work-items",
            new { title, parentId, statusId },
            actorLabel,
            ct)).Number;

    public Task AddUrlLinkAsync(Guid workItemId, string url, string? label, string actorLabel, string runId, CancellationToken ct) =>
        _rest.WithRunId(runId).WriteAsync(HttpMethod.Post, $"api/work-items/{workItemId}/url-links", new { url, label }, actorLabel, ct);

    public Task SetDatesAsync(
        Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, string runId, CancellationToken ct) =>
        _rest.WithRunId(runId).SetDatesAsync(workItemId, start, end, expectedVersion, actorLabel, ct);

    public Task AddCommentAsync(Guid workItemId, string body, string actorLabel, string runId, CancellationToken ct) =>
        _rest.WithRunId(runId).WriteAsync(HttpMethod.Post, $"api/work-items/{workItemId}/comments", new { body }, actorLabel, ct);
}
