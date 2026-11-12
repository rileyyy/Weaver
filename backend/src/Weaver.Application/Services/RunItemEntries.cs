using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One item's history entries from one run, oldest first, sorted into what an undo does with
/// each: the item's creation or deletion, the fields it changed, the links and URL links it
/// added, its comments, and the rest.
/// </summary>
internal sealed class RunItemEntries
{
    private static readonly HashSet<string> NonFieldEntries =
    [
        WorkItemHistoryField.Created,
        WorkItemHistoryField.Deleted,
        WorkItemHistoryField.Restored,
        WorkItemHistoryField.Purged,
        WorkItemHistoryField.Comment,
        WorkItemHistoryField.Link,
        WorkItemHistoryField.UrlLink,
        WorkItemHistoryField.Attachment,
        WorkItemHistoryField.Recurrence,
    ];

    public RunItemEntries(Guid workItemId, IReadOnlyList<WorkItemHistoryEntry> entries)
    {
        WorkItemId = workItemId;
        Entries = entries;
    }

    public Guid WorkItemId { get; }

    public IReadOnlyList<WorkItemHistoryEntry> Entries { get; }

    public bool Created => Entries.Any(e => e.Field == WorkItemHistoryField.Created);

    public bool Deleted => Entries.Any(e => e.Field == WorkItemHistoryField.Deleted);

    /// <summary>
    /// Each field the run changed, once, with the value it had before the run's first change and
    /// its first entry for it.
    /// </summary>
    public IEnumerable<RunFieldChange> FieldChanges =>
        Entries
            .Where(e => !NonFieldEntries.Contains(e.Field))
            .GroupBy(e => e.Field)
            .Select(g => new RunFieldChange(g.Key, g.First().OldValue, g.First().Id));

    /// <summary>Links the run added: entries with a new value and no old one.</summary>
    public IEnumerable<WorkItemHistoryEntry> AddedLinks => Added(WorkItemHistoryField.Link);

    public IEnumerable<WorkItemHistoryEntry> AddedUrlLinks => Added(WorkItemHistoryField.UrlLink);

    public IEnumerable<WorkItemHistoryEntry> Comments =>
        Entries.Where(e => e.Field == WorkItemHistoryField.Comment);

    /// <summary>
    /// What an undo doesn't put back: links and URL links the run removed, and attachment,
    /// repetition, restore and purge entries.
    /// </summary>
    public IEnumerable<WorkItemHistoryEntry> NotUndoable =>
        Entries.Where(e =>
            e.Field is WorkItemHistoryField.Restored
                or WorkItemHistoryField.Purged
                or WorkItemHistoryField.Attachment
                or WorkItemHistoryField.Recurrence
            || (e.Field is WorkItemHistoryField.Link or WorkItemHistoryField.UrlLink && !IsAddition(e)));

    private IEnumerable<WorkItemHistoryEntry> Added(string field) =>
        Entries.Where(e => e.Field == field && IsAddition(e));

    private static bool IsAddition(WorkItemHistoryEntry entry) => entry.OldValue is null && entry.NewValue is not null;
}
