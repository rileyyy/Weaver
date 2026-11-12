using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// History entries made on a run's items after the run started by anyone but the run, so an
/// undo can tell whether someone changed something since the run did.
/// </summary>
internal sealed class LaterChanges(IReadOnlyList<WorkItemHistoryEntry> entries)
{
    private readonly ILookup<Guid, WorkItemHistoryEntry> _byItem = entries.ToLookup(e => e.WorkItemId);

    public bool ToField(Guid workItemId, string field, long afterEntryId) =>
        After(workItemId, afterEntryId).Any(e => e.Field == field);

    /// <summary>Any later link entry between these two items, whichever side recorded it.</summary>
    public bool ToLink(Guid workItemId, Guid otherWorkItemId, long afterEntryId) =>
        After(workItemId, afterEntryId).Any(e =>
            e.Field == WorkItemHistoryField.Link
            && (HistoryJson.IdIn(e.OldValue) == otherWorkItemId || HistoryJson.IdIn(e.NewValue) == otherWorkItemId));

    /// <summary>A later entry for the same URL link: removed, or added again.</summary>
    public bool ToUrlLink(Guid workItemId, string valueJson, long afterEntryId) =>
        After(workItemId, afterEntryId).Any(e =>
            e.Field == WorkItemHistoryField.UrlLink && (e.OldValue == valueJson || e.NewValue == valueJson));

    /// <summary>Any later entry at all: the item is no longer only the run's.</summary>
    public bool ToItem(Guid workItemId, long afterEntryId) => After(workItemId, afterEntryId).Any();

    /// <summary>A later delete, restore or purge.</summary>
    public bool ToTrash(Guid workItemId, long afterEntryId) =>
        After(workItemId, afterEntryId).Any(e =>
            e.Field is WorkItemHistoryField.Deleted or WorkItemHistoryField.Restored or WorkItemHistoryField.Purged);

    private IEnumerable<WorkItemHistoryEntry> After(Guid workItemId, long afterEntryId) =>
        _byItem[workItemId].Where(e => e.Id > afterEntryId);
}
