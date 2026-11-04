using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemUrlLinkService(IWeaverDbContext db, ICallerContext caller) : IWorkItemUrlLinkService
{
    private readonly IWeaverDbContext _db = db;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public async Task<IReadOnlyList<WorkItemUrlLinkView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        await _db.WorkItemUrlLinks
            .Where(l => l.WorkItemId == workItemId)
            .OrderBy(l => l.CreatedAtUtc)
            .Select(l => new WorkItemUrlLinkView(l.Id, l.Url, l.Label))
            .ToListAsync(ct);

    public async Task<WorkItemUrlLinkView> CreateAsync(Guid workItemId, string url, string? label, CancellationToken ct = default)
    {
        var link = WorkItemUrlLink.Create(workItemId, url, label);

        if (!await _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }

        _db.WorkItemUrlLinks.Add(link);
        _history.Record(workItemId, WorkItemHistoryField.UrlLink, null, UrlLinkHistoryValue.Of(link));
        await _db.SaveChangesAsync(ct);
        return new WorkItemUrlLinkView(link.Id, link.Url, link.Label);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _db.WorkItemUrlLinks.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemUrlLink), id);

        _db.WorkItemUrlLinks.Remove(link);
        _history.Record(link.WorkItemId, WorkItemHistoryField.UrlLink, UrlLinkHistoryValue.Of(link), null);
        await _db.SaveChangesAsync(ct);
    }
}
