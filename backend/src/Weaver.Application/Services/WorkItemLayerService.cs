using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemLayerService : IWorkItemLayerService
{
    private readonly IWeaverDbContext _db;

    public WorkItemLayerService(IWeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WorkItemLayer>> GetAllAsync(CancellationToken ct = default) =>
        await _db.WorkItemLayers.AsNoTracking().OrderBy(l => l.Order).ToListAsync(ct);
}
