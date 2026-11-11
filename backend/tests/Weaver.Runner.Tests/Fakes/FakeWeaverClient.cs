using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>
/// Weaver in memory. Search matches an item when every word starts a word of its title or When,
/// as the server's full-text search does; Done items are never stored, since every read the
/// runner makes leaves them out.
/// </summary>
internal sealed class FakeWeaverClient : IWeaverClient
{
    public DateOnly Today { get; set; } = new(2026, 11, 11);

    public List<QuickPickRange> QuickPicks { get; } =
    [
        new("ThisWeekend", new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)),
        new("NextWeek", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22)),
    ];

    public List<WeaverWorkItem> Items { get; } = [];

    public List<(Guid From, Guid To)> Links { get; } = [];

    public List<string> Searches { get; } = [];

    public List<DateWrite> DateWrites { get; } = [];

    public List<LinkWrite> LinkWrites { get; } = [];

    public HashSet<Guid> RejectWritesTo { get; } = [];

    public HashSet<Guid> RejectLinkReadsFor { get; } = [];

    public Exception? FailEveryCallWith { get; set; }

    public WeaverWorkItem Add(string title, string? when = null, DateOnly? start = null, DateOnly? end = null, Guid? spaceId = null)
    {
        var item = new WeaverWorkItem(
            Guid.NewGuid(), Items.Count + 1, title, when, spaceId ?? Space, "Home", null, null, start, end, 7);
        Items.Add(item);
        return item;
    }

    public static readonly Guid Space = Guid.NewGuid();

    public Task<DateOnly> GetTodayAsync(CancellationToken ct) => Answer(() => Today);

    public Task<IReadOnlyList<QuickPickRange>> ListQuickPicksAsync(CancellationToken ct) =>
        Answer<IReadOnlyList<QuickPickRange>>(() => QuickPicks);

    public Task<IReadOnlyList<WeaverWorkItem>> ListUnresolvedWhenItemsAsync(CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverWorkItem>>(() => Items.Where(i => i.When is not null).ToList());

    public Task<IReadOnlyList<WeaverWorkItem>> SearchOpenItemsAsync(string text, int limit, CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverWorkItem>>(() =>
        {
            Searches.Add(text);
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return Items.Where(i => words.All(w => Matches(i, w))).Take(limit).ToList();
        });

    public Task<IReadOnlyList<Guid>> ListLinkedItemIdsAsync(Guid workItemId, CancellationToken ct) =>
        Answer<IReadOnlyList<Guid>>(() => RejectLinkReadsFor.Contains(workItemId)
            ? throw new WeaverRejectedException("404 Not Found")
            : Links
                .Where(l => l.From == workItemId || l.To == workItemId)
                .Select(l => l.From == workItemId ? l.To : l.From)
                .ToList());

    public Task SetDatesAsync(Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, CancellationToken ct) =>
        Answer(() =>
        {
            Reject(workItemId);
            DateWrites.Add(new DateWrite(workItemId, start, end, expectedVersion, actorLabel));
            return true;
        });

    public Task AddBlockedByLinkAsync(Guid workItemId, Guid blockerId, string actorLabel, CancellationToken ct) =>
        Answer(() =>
        {
            Reject(workItemId);
            LinkWrites.Add(new LinkWrite(workItemId, blockerId, actorLabel));
            return true;
        });

    private void Reject(Guid workItemId)
    {
        if (RejectWritesTo.Contains(workItemId))
        {
            throw new WeaverRejectedException("409 Conflict");
        }
    }

    private Task<T> Answer<T>(Func<T> answer) =>
        FailEveryCallWith is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());

    private static bool Matches(WeaverWorkItem item, string word) =>
        $"{item.Title} {item.When}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(w => w.StartsWith(word, StringComparison.OrdinalIgnoreCase));
}
