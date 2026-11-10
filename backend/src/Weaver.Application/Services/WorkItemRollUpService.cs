using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemRollUpService(IWeaverDbContext db, IWorkItemHierarchy hierarchy) : IWorkItemRollUpService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IWorkItemHierarchy _hierarchy = hierarchy;

    public async Task<IReadOnlyList<RollUpTotal>> GetAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await _db.WorkItems.Where(w => w.Id == itemId).Select(w => new { w.TypeId }).SingleOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException(nameof(WorkItem), itemId);
        var descendantIds = await _hierarchy.GetDescendantIdsAsync(itemId, ct);
        if (descendantIds.Count == 0)
        {
            return [];
        }

        var descendants = await _db.WorkItems.AsNoTracking().Where(w => descendantIds.Contains(w.Id)).ToListAsync(ct);
        var typeIds = descendants.Select(d => d.TypeId).OfType<Guid>().Distinct().ToList();
        var fields = await (
                from field in _db.CustomFields.AsNoTracking()
                join type in _db.WorkItemTypes on field.TypeId equals type.Id
                where field.RollsUp && typeIds.Contains(field.TypeId)
                orderby type.Order, field.Order
                select field)
            .ToListAsync(ct);
        // OrderBy is stable, so the other Types keep their own order behind the item's.
        return RollUp.Sum(descendants, fields.OrderBy(f => f.TypeId != item.TypeId));
    }
}
