using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemPriorityService : IWorkItemPriorityService
{
    private readonly IWeaverDbContext _db;

    public WorkItemPriorityService(IWeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkItemPriority>> GetAllAsync(CancellationToken ct = default) =>
        await _db.WorkItemPriorities.AsNoTracking().OrderBy(p => p.Order).ToListAsync(ct);
}
