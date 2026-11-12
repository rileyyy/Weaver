using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One undo of one run. It writes through the ordinary services, so every change records
/// history as the admin's, and it collects an outcome for each of the run's entries.
/// </summary>
/// <remarks>
/// The order matters. Items the run deleted come back first, so their fields and links can be
/// undone. Links go next, while both ends are still live: a trashed end hides a link. Items the
/// run created go to the Trash last, newest first, so each sub-item is its own delete and a
/// parent only takes along what someone else added under it.
/// </remarks>
internal sealed class RunUndoOperation(
    IWeaverDbContext db,
    IWorkItemService workItems,
    IWorkItemLinkService links,
    IWorkItemUrlLinkService urlLinks,
    IWorkItemTrashService trash,
    IReadOnlyList<RunItemEntries> items,
    LaterChanges later)
{
    private static readonly HashSet<string> DetailFields =
        [WorkItemField.Title, WorkItemField.Description, WorkItemField.When, WorkItemField.PriorityId];

    private static readonly HashSet<string> DateFields = [WorkItemField.StartDate, WorkItemField.EndDate];

    private readonly IWeaverDbContext _db = db;
    private readonly IWorkItemService _workItems = workItems;
    private readonly IWorkItemLinkService _links = links;
    private readonly IWorkItemUrlLinkService _urlLinks = urlLinks;
    private readonly IWorkItemTrashService _trash = trash;
    private readonly IReadOnlyList<RunItemEntries> _items = items;
    private readonly LaterChanges _later = later;
    private readonly List<RunEntryOutcome> _outcomes = [];

    /// <summary>One per entry of the run, in the order the run made them.</summary>
    public IReadOnlyList<RunEntryOutcome> Outcomes => _outcomes.OrderBy(o => o.EntryId).ToList();

    public async Task RunAsync(CancellationToken ct)
    {
        var created = _items.Where(i => i.Created).ToList();
        var existing = _items.Where(i => !i.Created).ToList();

        var deleted = existing.Where(i => i.Deleted).ToList();
        foreach (var item in deleted)
        {
            await RestoreAsync(item, ct);
        }

        foreach (var item in deleted)
        {
            await RecordRestoreAsync(item, ct);
        }

        foreach (var item in _items)
        {
            await RemoveLinksAsync(item, ct);
            await RemoveUrlLinksAsync(item, ct);
            KeepComments(item);
            Record(item.NotUndoable, RunUndoOutcome.NotUndoable);
        }

        foreach (var item in existing)
        {
            await RevertFieldsAsync(item, ct);
        }

        foreach (var item in Enumerable.Reverse(created))
        {
            await TrashCreatedAsync(item, ct);
        }
    }

    /// <summary>
    /// Only the item a delete was made on is restored; what went with it comes back too, and is
    /// judged afterwards by <see cref="RecordRestoreAsync"/>.
    /// </summary>
    private async Task RestoreAsync(RunItemEntries item, CancellationToken ct)
    {
        var row = await _db.WorkItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == item.WorkItemId, ct);
        if (!_later.ToTrash(item.WorkItemId, DeleteEntry(item).Id) && row is { IsTrashed: true, IsTrashRoot: true })
        {
            await _trash.RestoreAsync(item.WorkItemId, ct);
        }
    }

    private async Task RecordRestoreAsync(RunItemEntries item, CancellationToken ct)
    {
        var deleted = DeleteEntry(item);
        var outcome = await IsLiveAsync(item.WorkItemId, ct) ? RunUndoOutcome.Undone
            : _later.ToTrash(item.WorkItemId, deleted.Id) ? RunUndoOutcome.ChangedSince
            : RunUndoOutcome.ItemGone;
        Record(deleted, outcome);
    }

    private static WorkItemHistoryEntry DeleteEntry(RunItemEntries item) =>
        item.Entries.First(e => e.Field == WorkItemHistoryField.Deleted);

    /// <summary>
    /// A link is recorded on both items, so the second side finds it already gone and is
    /// reported the same as the first.
    /// </summary>
    private async Task RemoveLinksAsync(RunItemEntries item, CancellationToken ct)
    {
        foreach (var entry in item.AddedLinks)
        {
            Record(entry, await RemoveLinkAsync(item.WorkItemId, entry, ct));
        }
    }

    private async Task<RunUndoOutcome> RemoveLinkAsync(Guid workItemId, WorkItemHistoryEntry entry, CancellationToken ct)
    {
        if (HistoryValueNames.IdIn(entry.NewValue) is not { } otherId)
        {
            return RunUndoOutcome.NotUndoable;
        }

        if (_later.ToLink(workItemId, otherId, entry.Id) || _later.ToLink(otherId, workItemId, entry.Id))
        {
            return RunUndoOutcome.ChangedSince;
        }

        if (!await IsLiveAsync(workItemId, ct) || !await IsLiveAsync(otherId, ct))
        {
            return RunUndoOutcome.ItemGone;
        }

        var link = await _db.WorkItemLinks.AsNoTracking().FirstOrDefaultAsync(
            l => (l.WorkItemId == workItemId && l.LinkedWorkItemId == otherId) ||
                 (l.WorkItemId == otherId && l.LinkedWorkItemId == workItemId),
            ct);
        if (link is not null)
        {
            await _links.DeleteAsync(link.Id, ct);
        }

        return RunUndoOutcome.Undone;
    }

    private async Task RemoveUrlLinksAsync(RunItemEntries item, CancellationToken ct)
    {
        foreach (var entry in item.AddedUrlLinks)
        {
            Record(entry, await RemoveUrlLinkAsync(item.WorkItemId, entry, ct));
        }
    }

    private async Task<RunUndoOutcome> RemoveUrlLinkAsync(Guid workItemId, WorkItemHistoryEntry entry, CancellationToken ct)
    {
        if (_later.ToUrlLink(workItemId, entry.NewValue!, entry.Id))
        {
            return RunUndoOutcome.ChangedSince;
        }

        if (!await IsLiveAsync(workItemId, ct))
        {
            return RunUndoOutcome.ItemGone;
        }

        var value = HistoryJson.Deserialize<UrlLinkHistoryValue>(entry.NewValue)!;
        var link = await _db.WorkItemUrlLinks.AsNoTracking().FirstOrDefaultAsync(
            l => l.WorkItemId == workItemId && l.Url == value.Url && l.Label == value.Label,
            ct);
        if (link is null)
        {
            return RunUndoOutcome.ChangedSince;
        }

        await _urlLinks.DeleteAsync(link.Id, ct);
        return RunUndoOutcome.Undone;
    }

    private void KeepComments(RunItemEntries item) => Record(item.Comments, RunUndoOutcome.Kept);

    private async Task RevertFieldsAsync(RunItemEntries item, CancellationToken ct)
    {
        var changes = item.FieldChanges.ToList();
        if (changes.Count == 0)
        {
            return;
        }

        if (!await IsLiveAsync(item.WorkItemId, ct))
        {
            RecordFields(item, changes, RunUndoOutcome.ItemGone);
            return;
        }

        var revertible = new List<RunFieldChange>();
        foreach (var change in changes)
        {
            if (!IsRevertible(change.Field))
            {
                RecordFields(item, [change], RunUndoOutcome.NotUndoable);
            }
            else if (_later.ToField(item.WorkItemId, change.Field, change.FirstEntryId))
            {
                RecordFields(item, [change], RunUndoOutcome.ChangedSince);
            }
            else
            {
                revertible.Add(change);
            }
        }

        var datesOutcome = await RevertDatesAsync(item.WorkItemId, revertible.Where(c => DateFields.Contains(c.Field)).ToList(), ct);
        RecordFields(item, revertible.Where(c => DateFields.Contains(c.Field)).ToList(), datesOutcome);

        await RevertOthersAsync(item.WorkItemId, revertible.Where(c => !DateFields.Contains(c.Field)).ToList(), ct);
        RecordFields(item, revertible.Where(c => !DateFields.Contains(c.Field)).ToList(), RunUndoOutcome.Undone);
    }

    private static bool IsRevertible(string field) =>
        DetailFields.Contains(field)
        || DateFields.Contains(field)
        || field is WorkItemField.AssignedToUserId or WorkItemField.Tags;

    /// <summary>
    /// Both dates are put back in one write, and only if the item then still starts before it
    /// ends: someone may have moved the other date since.
    /// </summary>
    private async Task<RunUndoOutcome> RevertDatesAsync(Guid workItemId, IReadOnlyList<RunFieldChange> dates, CancellationToken ct)
    {
        if (dates.Count == 0)
        {
            return RunUndoOutcome.Undone;
        }

        var start = OldValue<DateOnly?>(dates, WorkItemField.StartDate);
        var end = OldValue<DateOnly?>(dates, WorkItemField.EndDate);
        var item = await _db.WorkItems.AsNoTracking().FirstAsync(w => w.Id == workItemId, ct);
        if (start.GetValueOr(item.StartDate) > end.GetValueOr(item.EndDate))
        {
            return RunUndoOutcome.InvalidDates;
        }

        await _workItems.RescheduleAsync(workItemId, start, end, ct: ct);
        return RunUndoOutcome.Undone;
    }

    private async Task RevertOthersAsync(Guid workItemId, IReadOnlyList<RunFieldChange> changes, CancellationToken ct)
    {
        if (changes.Any(c => DetailFields.Contains(c.Field)))
        {
            var title = OldValue<string>(changes, WorkItemField.Title);
            var details = new WorkItemDetailsPatch
            {
                Title = title.HasValue ? title.Value! : Optional<string>.Unset,
                Description = OldValue<string?>(changes, WorkItemField.Description),
                When = OldValue<string?>(changes, WorkItemField.When),
                PriorityId = OldValue<Guid?>(changes, WorkItemField.PriorityId),
            };
            await _workItems.UpdateDetailsAsync(workItemId, details, ct: ct);
        }

        if (OldValue<Guid?>(changes, WorkItemField.AssignedToUserId) is { HasValue: true } assignee)
        {
            await _workItems.AssignAsync(workItemId, assignee.Value, ct: ct);
        }

        if (OldValue<List<string>>(changes, WorkItemField.Tags) is { HasValue: true } tags)
        {
            await _workItems.SetTagsAsync(workItemId, new Optional<IReadOnlyList<string>>(tags.Value ?? []), ct: ct);
        }
    }

    /// <summary>The value before the run, or unset when the run didn't change <paramref name="field"/>.</summary>
    private static Optional<T?> OldValue<T>(IEnumerable<RunFieldChange> changes, string field) =>
        changes.FirstOrDefault(c => c.Field == field) is { } change
            ? new Optional<T?>(HistoryJson.Deserialize<T>(change.OldValueJson))
            : Optional<T?>.Unset;

    /// <summary>Every entry on an item the run created goes with it, except its comments, which are kept.</summary>
    private async Task TrashCreatedAsync(RunItemEntries item, CancellationToken ct)
    {
        var outcome = RunUndoOutcome.ItemGone;
        if (await IsLiveAsync(item.WorkItemId, ct))
        {
            await _trash.MoveToTrashAsync(item.WorkItemId, cascade: true, ct: ct);
            outcome = RunUndoOutcome.Undone;
        }

        var handled = _outcomes.Select(o => o.EntryId).ToHashSet();
        Record(item.Entries.Where(e => !handled.Contains(e.Id)), outcome);
    }

    private Task<bool> IsLiveAsync(Guid workItemId, CancellationToken ct) =>
        _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct);

    private void RecordFields(RunItemEntries item, IReadOnlyCollection<RunFieldChange> changes, RunUndoOutcome outcome)
    {
        var fields = changes.Select(c => c.Field).ToHashSet();
        Record(item.Entries.Where(e => fields.Contains(e.Field)), outcome);
    }

    private void Record(IEnumerable<WorkItemHistoryEntry> entries, RunUndoOutcome outcome)
    {
        foreach (var entry in entries)
        {
            Record(entry, outcome);
        }
    }

    private void Record(WorkItemHistoryEntry entry, RunUndoOutcome outcome) =>
        _outcomes.Add(new RunEntryOutcome(entry.Id, entry.WorkItemId, entry.Field, outcome));
}
