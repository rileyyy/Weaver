using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemLayerService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller) : LookupService<WorkItemLayer>(db, admins, caller), IWorkItemLayerService
{
    protected override DbSet<WorkItemLayer> Values => Db.WorkItemLayers;

    protected override string Kind => "Layer";

    public Task<WorkItemLayer> CreateAsync(Guid actorUserId, string name, string color, CancellationToken ct = default) =>
        AddAsync(actorUserId, order => WorkItemLayer.Create(name, order, color), ct);

    public Task<WorkItemLayer> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        string color,
        CancellationToken ct = default) =>
        EditAsync(actorUserId, id, layer =>
        {
            layer.Rename(name);
            layer.Recolor(color);
        }, ct);

    protected override async Task MoveWorkItemsAsync(WorkItemLayer deleted, Guid? replacementId, CancellationToken ct)
    {
        var items = await Db.WorkItems.Where(w => w.LayerId == deleted.Id).ToListAsync(ct);
        foreach (var item in items)
        {
            History.RecordChanges(item.Id, item.ChangeLayer(replacementId));
        }
    }
}
