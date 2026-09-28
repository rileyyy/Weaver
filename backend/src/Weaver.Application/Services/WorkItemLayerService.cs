using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemLayerService : LookupService<WorkItemLayer>, IWorkItemLayerService
{
    public WorkItemLayerService(IWeaverDbContext db, IAdminAuthorizer admins) : base(db, admins)
    {
    }

    protected override DbSet<WorkItemLayer> Values => Db.WorkItemLayers;

    protected override string Kind => "Layer";

    public Task<WorkItemLayer> CreateAsync(Guid actorUserId, string name, CancellationToken ct = default) =>
        AddAsync(actorUserId, order => WorkItemLayer.Create(name, order), ct);

    public Task<WorkItemLayer> RenameAsync(Guid actorUserId, Guid id, string name, CancellationToken ct = default) =>
        EditAsync(actorUserId, id, layer => layer.Rename(name), ct);

    protected override async Task MoveWorkItemsAsync(WorkItemLayer deleted, Guid? replacementId, CancellationToken ct)
    {
        var items = await Db.WorkItems.Where(w => w.LayerId == deleted.Id).ToListAsync(ct);
        foreach (var item in items)
        {
            item.ChangeLayer(replacementId);
        }
    }
}
