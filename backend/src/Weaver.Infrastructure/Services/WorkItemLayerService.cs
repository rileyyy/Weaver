using Microsoft.EntityFrameworkCore;
using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public class WorkItemLayerService : IWorkItemLayerService
{
    private readonly WeaverDbContext _db;

    public WorkItemLayerService(WeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkItemLayer>> GetAllAsync(CancellationToken ct = default) =>
        await _db.WorkItemLayers.OrderBy(l => l.Order).ToListAsync(ct);
}
