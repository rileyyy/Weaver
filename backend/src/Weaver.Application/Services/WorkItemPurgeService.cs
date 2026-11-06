using Microsoft.EntityFrameworkCore;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemPurgeService(
    IWeaverDbContext db,
    IAttachmentFileCleanup attachmentFiles,
    ICallerContext caller,
    TimeProvider clock) : IWorkItemPurgeService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IAttachmentFileCleanup _attachmentFiles = attachmentFiles;
    private readonly ICallerContext _caller = caller;
    private readonly TimeProvider _clock = clock;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public async Task PurgeAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureCallerIsHumanAsync(ct);

        var item = await _db.WorkItems.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), id);
        if (!item.IsTrashed)
        {
            throw new DomainValidationException($"Work item {id} isn't in the Trash.");
        }

        if (!item.IsTrashRoot)
        {
            throw new DomainValidationException(
                $"Work item {id} was deleted along with work item {item.TrashRootId}; purge that one instead.");
        }

        await PurgeDeletesAsync([id], _history.CurrentActor, ct);
    }

    public async Task EmptyTrashAsync(CancellationToken ct = default)
    {
        await EnsureCallerIsHumanAsync(ct);

        var rootIds = await TrashRoots().Select(w => w.Id).ToListAsync(ct);
        await PurgeDeletesAsync(rootIds, _history.CurrentActor, ct);
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct = default)
    {
        var settings = await _db.ServerSettings.AsNoTracking().SingleAsync(ct);
        var cutoff = settings.TrashExpiryCutoff(_clock.GetUtcNow());
        var rootIds = await TrashRoots().Where(w => w.TrashedAtUtc <= cutoff).Select(w => w.Id).ToListAsync(ct);
        return await PurgeDeletesAsync(rootIds, HistoryActor.System, ct);
    }

    /// <summary>
    /// Purges the deletes made on <paramref name="rootIds"/> in one transaction and returns how
    /// many items went. Attachment files are removed only once the rows are gone for good.
    /// </summary>
    private async Task<int> PurgeDeletesAsync(IReadOnlyCollection<Guid> rootIds, HistoryActor actor, CancellationToken ct)
    {
        var items = await Trash().Where(w => rootIds.Contains(w.TrashRootId!.Value)).ToListAsync(ct);
        if (items.Count == 0)
        {
            return 0;
        }

        var itemIds = items.Select(w => w.Id).ToList();
        await _attachmentFiles.PurgeAsync(
            itemIds,
            async rowsCt =>
            {
                await RemoveLinksAsync(itemIds, actor, rowsCt);
                await DetachSeparatelyDeletedChildrenAsync(itemIds, actor, rowsCt);
                await RemoveDependentsAsync(itemIds, rowsCt);
                foreach (var item in items)
                {
                    _history.Record(item.Id, actor, WorkItemHistoryField.Purged, item.Title, null);
                }

                _db.WorkItems.RemoveRange(items);
                await _db.SaveChangesAsync(rowsCt);
            },
            ct);
        return items.Count;
    }

    /// <summary>
    /// Both link FKs are Restrict. Trashing recorded nothing on the other side of a link, so an
    /// item that survives the purge records losing it now; a link between two purged items goes
    /// unrecorded, since each item's own purge says it all.
    /// </summary>
    private async Task RemoveLinksAsync(List<Guid> purgedIds, HistoryActor actor, CancellationToken ct)
    {
        var links = await _db.WorkItemLinks
            .IgnoreQueryFilters()
            .Where(l => purgedIds.Contains(l.WorkItemId) || purgedIds.Contains(l.LinkedWorkItemId))
            .ToListAsync(ct);
        foreach (var link in links)
        {
            foreach (var survivor in new[] { link.WorkItemId, link.LinkedWorkItemId }.Where(id => !purgedIds.Contains(id)))
            {
                var value = new LinkHistoryValue(link.OtherSideOf(survivor), link.RelationFrom(survivor));
                _history.Record(survivor, actor, WorkItemHistoryField.Link, value, null);
            }
        }

        _db.WorkItemLinks.RemoveRange(links);
    }

    /// <summary>
    /// A child deleted before its parent stays in the Trash in its own delete. The parent FK is
    /// Restrict, so it becomes a root of its Space now, which is where restoring it would put it
    /// once its parent is gone anyway.
    /// </summary>
    private async Task DetachSeparatelyDeletedChildrenAsync(List<Guid> purgedIds, HistoryActor actor, CancellationToken ct)
    {
        var children = await _db.WorkItems
            .IgnoreQueryFilters()
            .Where(w => w.ParentId != null && purgedIds.Contains(w.ParentId.Value) && !purgedIds.Contains(w.Id))
            .ToListAsync(ct);
        foreach (var child in children)
        {
            _history.RecordChanges(child.Id, actor, child.MoveToParent(null, child.Rank));
        }
    }

    /// <summary>
    /// These FKs cascade in the database, but removing the rows here keeps the purge from
    /// depending on what the provider enforces or what happens to be tracked.
    /// </summary>
    private async Task RemoveDependentsAsync(List<Guid> purgedIds, CancellationToken ct)
    {
        _db.Comments.RemoveRange(
            await _db.Comments.IgnoreQueryFilters().Where(c => purgedIds.Contains(c.WorkItemId)).ToListAsync(ct));
        _db.WorkItemUrlLinks.RemoveRange(
            await _db.WorkItemUrlLinks.IgnoreQueryFilters().Where(l => purgedIds.Contains(l.WorkItemId)).ToListAsync(ct));
        _db.WorkItemAttachments.RemoveRange(
            await _db.WorkItemAttachments.IgnoreQueryFilters().Where(a => purgedIds.Contains(a.WorkItemId)).ToListAsync(ct));
        _db.WorkItemRecurrences.RemoveRange(
            await _db.WorkItemRecurrences.IgnoreQueryFilters().Where(r => purgedIds.Contains(r.WorkItemId)).ToListAsync(ct));
    }

    /// <summary>
    /// Read from the account rather than the key's scope: a read-write agent passes every
    /// write check, but purging is for humans only (ADR-0007).
    /// </summary>
    private async Task EnsureCallerIsHumanAsync(CancellationToken ct)
    {
        var accountId = _caller.Current.AccountId;
        var kind = await _db.Users.Where(u => u.Id == accountId).Select(u => (UserKind?)u.Kind).FirstOrDefaultAsync(ct);
        if (kind != UserKind.Human)
        {
            throw new AgentPurgeException();
        }
    }

    private IQueryable<WorkItem> Trash() => _db.WorkItems.IgnoreQueryFilters().Where(w => w.TrashedAtUtc != null);

    private IQueryable<WorkItem> TrashRoots() => Trash().Where(w => w.TrashRootId == w.Id);
}
