using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class WorkItemPriorityService : LookupService<WorkItemPriority>, IWorkItemPriorityService
{
    public WorkItemPriorityService(IWeaverDbContext db, IAdminAuthorizer admins) : base(db, admins)
    {
    }

    protected override DbSet<WorkItemPriority> Values => Db.WorkItemPriorities;

    protected override string Kind => "Priority";

    public Task<WorkItemPriority> CreateAsync(Guid actorUserId, string name, CancellationToken ct = default) =>
        AddAsync(actorUserId, order => WorkItemPriority.Create(name, order), ct);

    public Task<WorkItemPriority> RenameAsync(Guid actorUserId, Guid id, string name, CancellationToken ct = default) =>
        EditAsync(actorUserId, id, priority => priority.Rename(name), ct);

    protected override async Task MoveWorkItemsAsync(WorkItemPriority deleted, Guid? replacementId, CancellationToken ct)
    {
        var items = await Db.WorkItems.Where(w => w.PriorityId == deleted.Id).ToListAsync(ct);
        foreach (var item in items)
        {
            item.ChangePriority(replacementId);
        }
    }
}
