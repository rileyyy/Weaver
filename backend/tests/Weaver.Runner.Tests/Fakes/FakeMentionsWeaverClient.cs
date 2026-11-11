using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>
/// Weaver in memory for the mentions task kind. A reply this agent posts clears the item's
/// pending mentions, as Weaver's pending rule does; every write is recorded with its actor label.
/// </summary>
internal sealed class FakeMentionsWeaverClient : IMentionsWeaverClient
{
    public static readonly Guid Agent = Guid.NewGuid();
    public static readonly Guid Riley = Guid.NewGuid();
    public static readonly Guid Space = Guid.NewGuid();
    public static readonly Guid ToDo = Guid.NewGuid();

    public DateOnly Today { get; set; } = new(2026, 11, 11);

    public List<WeaverItemDetail> Items { get; } = [];

    public List<(Guid WorkItemId, WeaverComment Comment)> Comments { get; } = [];

    public List<WeaverPendingMention> Pending { get; } = [];

    public List<(Guid WorkItemId, string Body, string ActorLabel)> Replies { get; } = [];

    public List<(Guid ParentId, string Title, Guid StatusId, string ActorLabel)> SubItems { get; } = [];

    public List<(Guid WorkItemId, string Url, string? Label, string ActorLabel)> UrlLinks { get; } = [];

    public List<DateWrite> DateWrites { get; } = [];

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
        var mention = new WeaverPendingMention(comment, item.Id, item.Number, item.Title);
        Pending.Add(mention);
        return mention;
    }

    public WeaverComment AddComment(Guid workItemId, Guid authorId, string author, string body)
    {
        var comment = new WeaverComment(Guid.NewGuid(), authorId, author, body, DateTimeOffset.UnixEpoch.AddMinutes(Comments.Count));
        Comments.Add((workItemId, comment));
        return comment;
    }

    public Task<DateOnly> GetTodayAsync(CancellationToken ct) => Answer(() => Today);

    public Task<IReadOnlyList<WeaverPendingMention>> ListPendingMentionsAsync(CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverPendingMention>>(() => Pending.ToList());

    public Task<WeaverItemDetail> GetItemAsync(Guid workItemId, CancellationToken ct) =>
        Answer(() => RejectItemReadsFor.Contains(workItemId)
            ? throw new WeaverRejectedException("404 Not Found")
            : Items.Single(i => i.Id == workItemId));

    public Task<IReadOnlyList<WeaverComment>> ListCommentsAsync(Guid workItemId, CancellationToken ct) =>
        Answer<IReadOnlyList<WeaverComment>>(() => Comments.Where(c => c.WorkItemId == workItemId).Select(c => c.Comment).ToList());

    public Task<Guid> GetFirstToDoStatusIdAsync(Guid spaceId, CancellationToken ct) => Answer(() => ToDo);

    public Task<int> AddSubItemAsync(Guid parentId, string title, Guid statusId, string actorLabel, CancellationToken ct) =>
        Answer(() =>
        {
            SubItems.Add((parentId, title, statusId, actorLabel));
            return 100 + SubItems.Count;
        });

    public Task AddUrlLinkAsync(Guid workItemId, string url, string? label, string actorLabel, CancellationToken ct) =>
        Answer(() =>
        {
            if (RejectUrls.Contains(url))
            {
                throw new WeaverRejectedException("400 Bad Request: that link is already on the item");
            }

            UrlLinks.Add((workItemId, url, label, actorLabel));
            return true;
        });

    public Task SetDatesAsync(Guid workItemId, DateOnly? start, DateOnly? end, uint expectedVersion, string actorLabel, CancellationToken ct) =>
        Answer(() =>
        {
            DateWrites.Add(new DateWrite(workItemId, start, end, expectedVersion, actorLabel));
            return true;
        });

    public Task AddCommentAsync(Guid workItemId, string body, string actorLabel, CancellationToken ct) =>
        Answer(() =>
        {
            if (RejectRepliesTo.Contains(workItemId))
            {
                throw new WeaverRejectedException("404 Not Found");
            }

            Replies.Add((workItemId, body, actorLabel));
            AddComment(workItemId, Agent, "Claude research", body);
            Pending.RemoveAll(p => p.WorkItemId == workItemId);
            return true;
        });

    private Task<T> Answer<T>(Func<T> answer) =>
        FailEveryCallWith is { } failure ? Task.FromException<T>(failure) : Task.FromResult(answer());
}
