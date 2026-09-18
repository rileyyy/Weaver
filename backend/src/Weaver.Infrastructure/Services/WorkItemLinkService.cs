using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Infrastructure.Services;

public class WorkItemLinkService : IWorkItemLinkService
{
    private readonly WeaverDbContext _db;

    public WorkItemLinkService(WeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkItemLink>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        await _db.WorkItemLinks
            .Include(l => l.WorkItem)
            .Include(l => l.LinkedWorkItem)
            .Where(l => l.WorkItemId == workItemId || l.LinkedWorkItemId == workItemId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<WorkItemLink> CreateAsync(Guid workItemId, Guid targetWorkItemId, CancellationToken ct = default)
    {
        if (workItemId == targetWorkItemId)
        {
            throw new SelfWorkItemLinkException(workItemId);
        }

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

        var link = new WorkItemLink
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            LinkedWorkItemId = targetWorkItemId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _db.WorkItemLinks.Add(link);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(link).Reference(l => l.LinkedWorkItem).LoadAsync(ct);
        await _db.Entry(link).Reference(l => l.WorkItem).LoadAsync(ct);
        return link;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _db.WorkItemLinks.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemLink), id);

        _db.WorkItemLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
    }
}
