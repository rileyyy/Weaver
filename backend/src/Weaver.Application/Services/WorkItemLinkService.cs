using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemLinkService : IWorkItemLinkService
{
    private readonly IWeaverDbContext _db;

    public WorkItemLinkService(IWeaverDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<WorkItemLinkView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        ProjectAsync(
            _db.WorkItemLinks
                .Where(l => l.WorkItemId == workItemId || l.LinkedWorkItemId == workItemId)
                .OrderBy(l => l.CreatedAtUtc),
            workItemId,
            ct);

    public async Task<WorkItemLinkView> CreateAsync(Guid workItemId, Guid targetWorkItemId, CancellationToken ct = default)
    {
        var link = WorkItemLink.Create(workItemId, targetWorkItemId);

        if (!await _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }

        if (!await _db.WorkItems.AnyAsync(w => w.Id == targetWorkItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), targetWorkItemId);
        }

        var alreadyLinked = await _db.WorkItemLinks.AnyAsync(
            l => (l.WorkItemId == workItemId && l.LinkedWorkItemId == targetWorkItemId) ||
                 (l.WorkItemId == targetWorkItemId && l.LinkedWorkItemId == workItemId),
            ct);
        if (alreadyLinked)
        {
            throw new DuplicateWorkItemLinkException(workItemId, targetWorkItemId);
        }

        _db.WorkItemLinks.Add(link);
        await _db.SaveChangesAsync(ct);
        return (await ProjectAsync(_db.WorkItemLinks.Where(l => l.Id == link.Id), workItemId, ct)).Single();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _db.WorkItemLinks.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemLink), id);

        _db.WorkItemLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Both titles are selected as columns and the "other side" is picked in memory, since
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
                l.WorkItemId,
                l.LinkedWorkItemId,
                WorkItemTitle = l.WorkItem!.Title,
                LinkedWorkItemTitle = l.LinkedWorkItem!.Title,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => r.WorkItemId == perspectiveWorkItemId
                ? new WorkItemLinkView(r.Id, r.LinkedWorkItemId, r.LinkedWorkItemTitle)
                : new WorkItemLinkView(r.Id, r.WorkItemId, r.WorkItemTitle))
            .ToList();
    }
}
