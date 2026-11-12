namespace Weaver.Runner.WeaverApi;

/// <summary>
/// Weaver's REST API as the mentions task kind uses it, with the same failure rules as
/// <see cref="IWeaverClient"/>: <see cref="WeaverUnavailableException"/> stops the run and
/// <see cref="WeaverRejectedException"/> is about one request. Every write is tagged with the
/// <c>runId</c> given, which groups it in history and the run reports.
/// </summary>
public interface IMentionsWeaverClient
{
    /// <summary>Today in the server's time zone, which every date rule in Weaver uses.</summary>
    Task<DateOnly> GetTodayAsync(CancellationToken ct);

    /// <summary>The comments mentioning this agent that it hasn't replied to yet, oldest first.</summary>
    Task<IReadOnlyList<WeaverPendingMention>> ListPendingMentionsAsync(CancellationToken ct);

    Task<WeaverItemDetail> GetItemAsync(Guid workItemId, CancellationToken ct);

    /// <summary>The item's comments, oldest first.</summary>
    Task<IReadOnlyList<WeaverComment>> ListCommentsAsync(Guid workItemId, CancellationToken ct);

    /// <summary>Every change to the item, newest first.</summary>
    Task<IReadOnlyList<WeaverHistoryEntry>> ListHistoryAsync(Guid workItemId, CancellationToken ct);

    /// <summary>The item's direct children.</summary>
    Task<IReadOnlyList<WeaverSubItem>> ListSubItemsAsync(Guid parentId, CancellationToken ct);

    /// <summary>The Space's first To Do status, which a new sub-item starts in.</summary>
    Task<Guid> GetFirstToDoStatusIdAsync(Guid spaceId, CancellationToken ct);

    /// <summary>Adds a sub-item under <paramref name="parentId"/> and returns its Number.</summary>
    Task<int> AddSubItemAsync(Guid parentId, string title, Guid statusId, string actorLabel, string runId, CancellationToken ct);

    Task AddUrlLinkAsync(Guid workItemId, string url, string? label, string actorLabel, string runId, CancellationToken ct);

    /// <summary>As <see cref="IWeaverClient.SetDatesAsync"/>.</summary>
    Task SetDatesAsync(
        Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, string runId, CancellationToken ct);

    Task AddCommentAsync(Guid workItemId, string body, string actorLabel, string runId, CancellationToken ct);
}
