using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemPriorityService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller) : SpaceLookupService<WorkItemPriority>(db, admins, caller), IWorkItemPriorityService
{
    protected override DbSet<WorkItemPriority> Values => Db.WorkItemPriorities;

    protected override string Kind => "Priority";

    public Task<WorkItemPriority> CreateAsync(Guid actorUserId, Guid spaceId, string name, CancellationToken ct = default) =>
        AddAsync(actorUserId, spaceId, order => WorkItemPriority.Create(spaceId, name, order), ct);

    public Task<WorkItemPriority> RenameAsync(Guid actorUserId, Guid id, string name, CancellationToken ct = default) =>
        EditAsync(actorUserId, id, priority => priority.Rename(name), ct);

    protected override async Task MoveWorkItemsAsync(WorkItemPriority deleted, Guid? replacementId, CancellationToken ct)
    {
        // Trashed items too: one restored later mustn't point at a value that's gone.
        var items = await Db.WorkItems.IgnoreQueryFilters().Where(w => w.PriorityId == deleted.Id).ToListAsync(ct);
        foreach (var item in items)
        {
            History.RecordChanges(item.Id, item.ChangePriority(replacementId));
        }
    }
}
