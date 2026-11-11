namespace Weaver.Runner.WeaverApi;

/// <summary>
/// Weaver's REST API as one agent key sees it. A failure that makes the run pointless (Weaver
/// unreachable, the key refused) throws <see cref="WeaverUnavailableException"/>; a write Weaver
/// turns down for one item throws <see cref="WeaverRejectedException"/>.
/// </summary>
public interface IWeaverClient
{
    /// <summary>Today in the server's time zone, which every date rule in Weaver uses.</summary>
    Task<DateOnly> GetTodayAsync(CancellationToken ct);

    Task<IReadOnlyList<QuickPickRange>> ListQuickPicksAsync(CancellationToken ct);

    /// <summary>Every item whose When is unresolved, leaving out Done items.</summary>
    Task<IReadOnlyList<WeaverWorkItem>> ListUnresolvedWhenItemsAsync(CancellationToken ct);

    /// <summary>Items that aren't Done and match every word of <paramref name="text"/>.</summary>
    Task<IReadOnlyList<WeaverWorkItem>> SearchOpenItemsAsync(string text, int limit, CancellationToken ct);

    /// <summary>The other side of every link the item already has, of any type.</summary>
    Task<IReadOnlyList<Guid>> ListLinkedItemIdsAsync(Guid workItemId, CancellationToken ct);

    /// <summary>
    /// Sets only the dates given, leaving the other unchanged, and only if the item is still at
    /// <paramref name="expectedVersion"/>, so an edit made since it was read isn't overwritten.
    /// </summary>
    Task SetDatesAsync(Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, CancellationToken ct);

    Task AddBlockedByLinkAsync(Guid workItemId, Guid blockerId, string actorLabel, CancellationToken ct);
}
