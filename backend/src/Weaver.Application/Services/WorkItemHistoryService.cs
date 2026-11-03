using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemHistoryService(IWeaverDbContext db) : IWorkItemHistoryService
{
    private readonly IWeaverDbContext _db = db;

    public async Task<IReadOnlyList<WorkItemHistoryView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var entries = await HistoryViewReader.ReadAsync(
            _db,
            _db.WorkItemHistory.Where(e => e.WorkItemId == workItemId).OrderByDescending(e => e.Id),
            ct);

        if (entries.Count == 0 && !await _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }

        return entries;
    }
}
