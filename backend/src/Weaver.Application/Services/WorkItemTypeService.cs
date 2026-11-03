using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemTypeService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller) : SpaceLookupService<WorkItemType>(db, admins, caller), IWorkItemTypeService
{
    protected override DbSet<WorkItemType> Values => Db.WorkItemTypes;

    protected override string Kind => "Type";

    public Task<WorkItemType> CreateAsync(
        Guid actorUserId,
        Guid spaceId,
        string name,
        string color,
        CancellationToken ct = default) =>
        AddAsync(actorUserId, spaceId, order => WorkItemType.Create(spaceId, name, order, color), ct);

    public Task<WorkItemType> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        string color,
        CancellationToken ct = default) =>
        EditAsync(actorUserId, id, type =>
        {
            type.Rename(name);
            type.Recolor(color);
        }, ct);

    protected override async Task MoveWorkItemsAsync(WorkItemType deleted, Guid? replacementId, CancellationToken ct)
    {
        var items = await Db.WorkItems.Where(w => w.TypeId == deleted.Id).ToListAsync(ct);
        foreach (var item in items)
        {
            History.RecordChanges(item.Id, item.ChangeType(replacementId));
        }
    }
}
