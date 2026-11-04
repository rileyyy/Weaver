using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemLinkService(IWeaverDbContext db, ICallerContext caller) : IWorkItemLinkService
{
    private readonly IWeaverDbContext _db = db;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public Task<IReadOnlyList<WorkItemLinkView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        ProjectAsync(
            _db.WorkItemLinks
                .Where(l => l.WorkItemId == workItemId || l.LinkedWorkItemId == workItemId)
                .OrderBy(l => l.CreatedAtUtc),
            workItemId,
            ct);

    public async Task<WorkItemLinkView> CreateAsync(
        Guid workItemId,
        Guid targetWorkItemId,
        WorkItemLinkRelation type,
        CancellationToken ct = default)
    {
        var link = WorkItemLink.Create(workItemId, targetWorkItemId, type);

        await _db.EnsureWorkItemExistsAsync(workItemId, ct);
        await _db.EnsureWorkItemExistsAsync(targetWorkItemId, ct);
        await EnsureNotLinkedAsync(link, ct);

        _db.WorkItemLinks.Add(link);
        RecordLinkChange(link, added: true);
        await _db.SaveChangesAsync(ct);
        return (await ProjectAsync(_db.WorkItemLinks.Where(l => l.Id == link.Id), workItemId, ct)).Single();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _db.WorkItemLinks.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemLink), id);

        _db.WorkItemLinks.Remove(link);
        RecordLinkChange(link, added: false);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The cycle is reported before the duplicate, since "already linked" wouldn't tell the
    /// caller that the order they asked for is the opposite of the one that exists.
    /// </summary>
    private async Task EnsureNotLinkedAsync(WorkItemLink link, CancellationToken ct)
    {
        var existing = await _db.WorkItemLinks.FirstOrDefaultAsync(
            l => (l.WorkItemId == link.WorkItemId && l.LinkedWorkItemId == link.LinkedWorkItemId) ||
                 (l.WorkItemId == link.LinkedWorkItemId && l.LinkedWorkItemId == link.WorkItemId),
            ct);
        if (existing is null)
        {
            return;
        }

        if (link.IsReverseBlocksOf(existing))
        {
            throw new BlockingCycleException(link.WorkItemId, link.LinkedWorkItemId);
        }

        throw new DuplicateWorkItemLinkException(link.WorkItemId, link.LinkedWorkItemId);
    }

    /// <summary>Each item records the other, and the type as it reads from its own side.</summary>
    private void RecordLinkChange(WorkItemLink link, bool added)
    {
        foreach (var side in new[] { link.WorkItemId, link.LinkedWorkItemId })
        {
            var value = new LinkHistoryValue(link.OtherSideOf(side), link.RelationFrom(side));
            _history.Record(side, WorkItemHistoryField.Link, added ? null : value, added ? value : null);
        }
    }

    /// <summary>
    /// Both sides are selected as columns and the "other side" is picked in memory, since
    /// which side that is depends on the perspective, not on anything EF can translate.
    /// </summary>
    private static async Task<IReadOnlyList<WorkItemLinkView>> ProjectAsync(
        IQueryable<WorkItemLink> links,
        Guid perspectiveWorkItemId,
        CancellationToken ct)
    {
        var rows = await links
            .Select(l => new
            {
                l.Id,
                l.Type,
                l.WorkItemId,
                WorkItemNumber = l.WorkItem!.Number,
                WorkItemTitle = l.WorkItem!.Title,
                WorkItemSpaceId = l.WorkItem!.SpaceId,
                l.LinkedWorkItemId,
                LinkedWorkItemNumber = l.LinkedWorkItem!.Number,
                LinkedWorkItemTitle = l.LinkedWorkItem!.Title,
                LinkedWorkItemSpaceId = l.LinkedWorkItem!.SpaceId,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => r.WorkItemId == perspectiveWorkItemId
                ? new WorkItemLinkView(
                    r.Id,
                    WorkItemLink.RelationFrom(r.Type, isSource: true),
                    r.LinkedWorkItemId,
                    r.LinkedWorkItemNumber,
                    r.LinkedWorkItemTitle,
                    r.LinkedWorkItemSpaceId)
                : new WorkItemLinkView(
                    r.Id,
                    WorkItemLink.RelationFrom(r.Type, isSource: false),
                    r.WorkItemId,
                    r.WorkItemNumber,
                    r.WorkItemTitle,
                    r.WorkItemSpaceId))
            .ToList();
    }
}
