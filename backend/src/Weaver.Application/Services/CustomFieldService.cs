using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// A Type's fields share a lookup's rules (names unique and order kept within the Type, admin
/// only), so this builds on <see cref="LookupService{T}"/> with the Type as the siblings' scope.
/// </summary>
public class CustomFieldService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller)
    : LookupService<CustomFieldDefinition>(db, admins, caller), ICustomFieldService
{
    protected override DbSet<CustomFieldDefinition> Values => Db.CustomFields;

    protected override string Kind => "Field";

    public Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(Guid typeId, CancellationToken ct = default) =>
        ListAsync(InType(typeId), ct);

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(
        IReadOnlyCollection<Guid> typeIds,
        CancellationToken ct = default) =>
        await Values.AsNoTracking()
            .Where(f => typeIds.Contains(f.TypeId))
            .OrderBy(f => f.TypeId)
            .ThenBy(f => f.Order)
            .ToListAsync(ct);

    public async Task<CustomFieldDefinition> CreateAsync(
        Guid actorUserId,
        Guid typeId,
        string name,
        CustomFieldKind kind,
        IReadOnlyList<string> optionNames,
        CancellationToken ct = default)
    {
        if (!await Db.WorkItemTypes.AnyAsync(t => t.Id == typeId, ct))
        {
            throw new EntityNotFoundException("Type", typeId);
        }

        return await AddWithinAsync(
            actorUserId, InType(typeId), order => CustomFieldDefinition.Create(typeId, name, kind, order, optionNames), ct);
    }

    public Task<CustomFieldDefinition> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        IReadOnlyList<CustomFieldOptionRevision> options,
        CancellationToken ct = default) =>
        EditAsync(actorUserId, id, async field =>
        {
            field.Rename(name);
            field.ReviseOptions(options);
            foreach (var item in await ItemsOfTypeAsync(field.TypeId, ct))
            {
                History.RecordChanges(item.Id, item.PruneCustomField(field));
            }
        }, ct);

    public Task<IReadOnlyList<CustomFieldDefinition>> ReorderAsync(
        Guid actorUserId,
        Guid typeId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default) =>
        ReorderWithinAsync(actorUserId, InType(typeId), orderedIds, ct);

    public Task DeleteAsync(Guid actorUserId, Guid id, CancellationToken ct = default) =>
        DeleteAsync(actorUserId, id, replacementId: null, ct);

    protected override IQueryable<CustomFieldDefinition> SiblingsOf(CustomFieldDefinition value) => InType(value.TypeId);

    /// <summary>
    /// A field's values are of its own kind and options, so they can't move onto another field:
    /// they're removed, and history keeps them.
    /// </summary>
    protected override async Task MoveWorkItemsAsync(CustomFieldDefinition deleted, Guid? replacementId, CancellationToken ct)
    {
        if (replacementId is not null)
        {
            throw new DomainValidationException("A field's values can't move to another field.");
        }

        foreach (var item in await ItemsOfTypeAsync(deleted.TypeId, ct))
        {
            History.RecordChanges(item.Id, item.RemoveCustomField(deleted.Id));
        }
    }

    /// <summary>
    /// Every item of the Type, tracked. Which of them hold a value is checked in memory: that
    /// keeps the query portable, and a Type's items are few enough for a household server.
    /// </summary>
    private async Task<List<WorkItem>> ItemsOfTypeAsync(Guid typeId, CancellationToken ct) =>
        await Db.WorkItems.Where(w => w.TypeId == typeId).ToListAsync(ct);

    private IQueryable<CustomFieldDefinition> InType(Guid typeId) => Values.Where(f => f.TypeId == typeId);
}
