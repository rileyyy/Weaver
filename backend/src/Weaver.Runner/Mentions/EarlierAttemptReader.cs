using System.Text.Json;
using Weaver.Runner.Model;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Mentions;

/// <summary>
/// Reads an <see cref="EarlierAttempt"/> from Weaver's history. Every write answering a mention
/// carries that mention's run id (<see cref="MentionsRun.RunIdFor"/>), whichever run made it, so
/// the history entries with that id are the earlier attempts' additions. Only entries since the
/// mention appeared count: a mention edited back into a comment after a reply is a new request.
/// </summary>
internal sealed class EarlierAttemptReader(IMentionsWeaverClient weaver)
{
    private const string CreatedField = "created";
    private const string StartDateField = "startDate";
    private const string EndDateField = "endDate";
    private const string UrlLinkField = "urlLink";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMentionsWeaverClient _weaver = weaver;

    public async Task<EarlierAttempt> ReadAsync(WeaverPendingMention mention, string runId, CancellationToken ct)
    {
        bool IsEarlierAttempts(WeaverHistoryEntry entry) =>
            entry.RunId == runId && entry.ChangedAtUtc >= mention.MentionedAtUtc;

        var itemHistory = (await _weaver.ListHistoryAsync(mention.WorkItemId, ct)).Where(IsEarlierAttempts).ToList();
        var subItems = await ReadSubItemsAsync(mention, IsEarlierAttempts, ct);
        return new EarlierAttempt(ReadDates(itemHistory), subItems, ReadLinks(itemHistory));
    }

    /// <summary>
    /// A sub-item's creation is recorded on the sub-item, not its parent, so the children made
    /// since the mention each have their history read. Usually there are none.
    /// </summary>
    private async Task<IReadOnlyList<WeaverSubItem>> ReadSubItemsAsync(
        WeaverPendingMention mention, Func<WeaverHistoryEntry, bool> isEarlierAttempts, CancellationToken ct)
    {
        var madeSinceTheMention = (await _weaver.ListSubItemsAsync(mention.WorkItemId, ct))
            .Where(c => c.CreatedAtUtc >= mention.MentionedAtUtc)
            .OrderBy(c => c.Number);
        var subItems = new List<WeaverSubItem>();
        foreach (var child in madeSinceTheMention)
        {
            var history = await _weaver.ListHistoryAsync(child.Id, ct);
            if (history.Any(e => e.Field == CreatedField && isEarlierAttempts(e)))
            {
                subItems.Add(child);
            }
        }

        return subItems;
    }

    /// <summary>History is newest first, so the first entry for each date is the value it was last set to.</summary>
    private static (DateOnly? Start, DateOnly? End)? ReadDates(IReadOnlyList<WeaverHistoryEntry> history)
    {
        var start = history.FirstOrDefault(e => e.Field == StartDateField);
        var end = history.FirstOrDefault(e => e.Field == EndDateField);
        if (start is null && end is null)
        {
            return null;
        }

        return (DateIn(start), DateIn(end));
    }

    private static DateOnly? DateIn(WeaverHistoryEntry? entry) =>
        entry?.NewValue is { ValueKind: JsonValueKind.String } value && IsoDate.TryParse(value.GetString(), out var date)
            ? date
            : null;

    private static IReadOnlyList<MentionLink> ReadLinks(IReadOnlyList<WeaverHistoryEntry> history) =>
        history
            .Where(e => e.Field == UrlLinkField && e.NewValue is { ValueKind: JsonValueKind.Object })
            .Select(e => e.NewValue!.Value.Deserialize<MentionLink>(Json))
            .OfType<MentionLink>()
            .Reverse()
            .ToList();
}
