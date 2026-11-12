using System.Text.Json;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>
/// Weaver in memory for the mentions task kind. A reply this agent posts clears the item's
/// pending mentions, as Weaver's pending rule does; every write is recorded with its actor label
/// and run id, and in history as Weaver records it. Each event is a minute after the last, so
/// history and creation times order as they would on a server.
/// </summary>
internal sealed class FakeMentionsWeaverClient : IMentionsWeaverClient
{
    public static readonly Guid Agent = Guid.NewGuid();
    public static readonly Guid Riley = Guid.NewGuid();
    public static readonly Guid Space = Guid.NewGuid();
    public static readonly Guid ToDo = Guid.NewGuid();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private DateTimeOffset _clock = DateTimeOffset.UnixEpoch;

    public DateOnly Today { get; set; } = new(2026, 11, 11);

    public List<WeaverItemDetail> Items { get; } = [];

    public List<(Guid WorkItemId, WeaverComment Comment)> Comments { get; } = [];

    public List<WeaverPendingMention> Pending { get; } = [];

    public List<(Guid WorkItemId, string Body, string ActorLabel, string RunId)> Replies { get; } = [];

    public List<(Guid ParentId, string Title, Guid StatusId, string ActorLabel, string RunId)> SubItems { get; } = [];

    public List<(Guid WorkItemId, string Url, string? Label, string ActorLabel, string RunId)> UrlLinks { get; } = [];

    public List<DateWrite> DateWrites { get; } = [];

    /// <summary>Every item's children, whoever added them.</summary>
    public List<(Guid ParentId, WeaverSubItem SubItem)> Children { get; } = [];

    public List<(Guid WorkItemId, WeaverHistoryEntry Entry)> History { get; } = [];

    public HashSet<Guid> RejectRepliesTo { get; } = [];

    public HashSet<Guid> RejectItemReadsFor { get; } = [];

    public HashSet<string> RejectUrls { get; } = [];

    public Exception? FailEveryCallWith { get; set; }

    public WeaverItemDetail AddItem(string title, DateOnly? start = null, DateOnly? end = null, string? description = null)
    {
        var item = new WeaverItemDetail(
            Guid.NewGuid(), Items.Count + 1, title, description, Space, "Home", "To Do", null, start, end, 5);
        Items.Add(item);
        return item;
    }

    public WeaverPendingMention Mention(WeaverItemDetail item, string body)
    {
        var comment = AddComment(item.Id, Riley, "riley", body);
        return Remention(item, comment);
    }

    /// <summary>A mention of <paramref name="comment"/> appearing now, as when an edit adds it back after a reply.</summary>
    public WeaverPendingMention Remention(WeaverItemDetail item, WeaverComment comment)
    {
        var mention = new WeaverPendingMention(comment, item.Id, item.Number, item.Title, Tick());
        Pending.Add(mention);
        return mention;
    }

    public WeaverComment AddComment(Guid workItemId, Guid authorId, string author, string body)
    {
        var comment = new WeaverComment(Guid.NewGuid(), authorId, author, body, Tick());
        Comments.Add((workItemId, comment));
        return comment;
    }

    /// <summary>A sub-item someone else adds, outside any of the agent's runs.</summary>
    public WeaverSubItem AddChildByHand(Guid parentId, string title) => AddChild(parentId, title, runId: null);

    public Task<DateOnly> GetTodayAsync(CancellationToken ct) => Answer(() => Today);

    public Task<IReadOnlyList<WeaverPendingMention>> ListPendingMentionsAsync(CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverPendingMention>>(() => Pending.ToList());

    public Task<WeaverItemDetail> GetItemAsync(Guid workItemId, CancellationToken ct) =>
        Answer(() => RejectItemReadsFor.Contains(workItemId)
            ? throw new WeaverRejectedException("404 Not Found")
            : Items.Single(i => i.Id == workItemId));

    public Task<IReadOnlyList<WeaverComment>> ListCommentsAsync(Guid workItemId, CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverComment>>(() => Comments.Where(c => c.WorkItemId == workItemId).Select(c => c.Comment).ToList());

    public Task<IReadOnlyList<WeaverHistoryEntry>> ListHistoryAsync(Guid workItemId, CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverHistoryEntry>>(() =>
            History.Where(h => h.WorkItemId == workItemId).Select(h => h.Entry).Reverse().ToList());

    public Task<IReadOnlyList<WeaverSubItem>> ListSubItemsAsync(Guid parentId, CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverSubItem>>(() => Children.Where(c => c.ParentId == parentId).Select(c => c.SubItem).ToList());

    public Task<Guid> GetFirstToDoStatusIdAsync(Guid spaceId, CancellationToken ct) => Answer(() => ToDo);

    public Task<int> AddSubItemAsync(
        Guid parentId, string title, Guid statusId, string actorLabel, string runId, CancellationToken ct) =>
        Answer(() =>
        {
            SubItems.Add((parentId, title, statusId, actorLabel, runId));
            return AddChild(parentId, title, runId).Number;
        });

    public Task AddUrlLinkAsync(Guid workItemId, string url, string? label, string actorLabel, string runId, CancellationToken ct) =>
        Answer(() =>
        {
            if (RejectUrls.Contains(url))
            {
                throw new WeaverRejectedException("400 Bad Request: that link is already on the item");
            }

            UrlLinks.Add((workItemId, url, label, actorLabel, runId));
            Record(workItemId, runId, "urlLink", new { url, label });
            return true;
        });

    public Task SetDatesAsync(
        Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, string runId, CancellationToken ct) =>
        Answer(() =>
        {
            DateWrites.Add(new DateWrite(workItemId, start, end, expectedVersion, actorLabel));
            var index = Items.FindIndex(i => i.Id == workItemId);
            var item = Items[index];
            Items[index] = item with
            {
                StartDate = start ?? item.StartDate,
                EndDate = end ?? item.EndDate,
                Version = item.Version + 1,
            };
            if (start is not null)
            {
                Record(workItemId, runId, "startDate", start);
            }

            if (end is not null)
            {
                Record(workItemId, runId, "endDate", end);
            }

            return true;
        });

    public Task AddCommentAsync(Guid workItemId, string body, string actorLabel, string runId, CancellationToken ct) =>
        Answer(() =>
        {
            if (RejectRepliesTo.Contains(workItemId))
            {
                throw new WeaverRejectedException("404 Not Found");
            }

            Replies.Add((workItemId, body, actorLabel, runId));
            AddComment(workItemId, Agent, "Claude research", body);
            Record(workItemId, runId, "comment", body);
            Pending.RemoveAll(p => p.WorkItemId == workItemId);
            return true;
        });

    private WeaverSubItem AddChild(Guid parentId, string title, string? runId)
    {
        var child = new WeaverSubItem(Guid.NewGuid(), 101 + Children.Count, title, Tick());
        Children.Add((parentId, child));
        Record(child.Id, runId, "created", title);
        return child;
    }

    private void Record(Guid workItemId, string? runId, string field, object newValue) =>
        History.Add((workItemId, new WeaverHistoryEntry(Tick(), runId, field, JsonSerializer.SerializeToElement(newValue, Json))));

    private DateTimeOffset Tick() => _clock = _clock.AddMinutes(1);

    private Task<T> Answer<T>(Func<T> answer) =>
        FailEveryCallWith is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}
