using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// The admin operations every lookup shares: listing, adding at the end, editing, reordering
/// and deleting with a replacement. Subclasses say which table they use and how work items
/// are moved off a deleted value.
/// </summary>
public abstract class LookupService<T> : ILookupService<T> where T : LookupValue
{
    private readonly IAdminAuthorizer _admins;

    protected LookupService(IWeaverDbContext db, IAdminAuthorizer admins)
    {
        Db = db;
        _admins = admins;
    }

    protected IWeaverDbContext Db { get; }

    protected abstract DbSet<T> Values { get; }

    /// <summary>The kind's display name, for error messages ("Status", "Priority").</summary>
    protected abstract string Kind { get; }

    /// <summary>How many values must remain; deleting below it is refused.</summary>
    protected virtual int MinimumCount => 0;

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await Values.AsNoTracking().OrderBy(v => v.Order).ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ReorderAsync(
        Guid actorUserId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var values = await Values.ToDictionaryAsync(v => v.Id, ct);
        if (orderedIds.Count != values.Count || orderedIds.Distinct().Count() != values.Count
            || !orderedIds.All(values.ContainsKey))
        {
            throw new DomainValidationException($"The new order must list every {Kind.ToLowerInvariant()} exactly once.");
        }

        // Order has a unique index and Postgres checks it row by row, so writing the final
        // positions directly fails as soon as two values swap. Park every value on a negative
        // order first (never used otherwise), then write the real ones.
        await using var transaction = await Db.BeginTransactionAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            values[orderedIds[i]].MoveTo(-(i + 1));
        }
        await Db.SaveChangesAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            values[orderedIds[i]].MoveTo(i);
        }
        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return orderedIds.Select(id => values[id]).ToList();
    }

    public async Task DeleteAsync(Guid actorUserId, Guid id, Guid? replacementId, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var value = await Values.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(Kind, id);

        if (replacementId == id)
        {
            throw new DomainValidationException($"A {Kind.ToLowerInvariant()} can't be replaced by itself.");
        }

        if (replacementId is { } replacement && !await Values.AnyAsync(v => v.Id == replacement, ct))
        {
            throw new EntityNotFoundException(Kind, replacement);
        }

        if (await Values.CountAsync(ct) <= MinimumCount)
        {
            throw new LastLookupValueException(Kind);
        }

        await MoveWorkItemsAsync(value, replacementId, ct);
        Values.Remove(value);
        await Db.SaveChangesAsync(ct);
    }

    /// <summary>Appends a new value after the current last one.</summary>
    protected async Task<T> AddAsync(Guid actorUserId, Func<int, T> create, CancellationToken ct)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var lastOrder = await Values.MaxAsync(v => (int?)v.Order, ct);
        var value = create(lastOrder is null ? 0 : lastOrder.Value + 1);
        await EnsureNameFreeAsync(value, ct);

        Values.Add(value);
        await Db.SaveChangesAsync(ct);
        return value;
    }

    protected async Task<T> EditAsync(Guid actorUserId, Guid id, Action<T> edit, CancellationToken ct)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var value = await Values.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(Kind, id);

        edit(value);
        await EnsureNameFreeAsync(value, ct);

        await Db.SaveChangesAsync(ct);
        return value;
    }

    /// <summary>
    /// Moves every work item using <paramref name="deleted"/> onto the replacement, tracked so
    /// it is saved together with the delete.
    /// </summary>
    protected abstract Task MoveWorkItemsAsync(T deleted, Guid? replacementId, CancellationToken ct);

    /// <summary>Two values with the same name would be indistinguishable in every picker.</summary>
    private async Task EnsureNameFreeAsync(T value, CancellationToken ct)
    {
        var name = value.Name.ToLower();
        if (await Values.AnyAsync(v => v.Id != value.Id && v.Name.ToLower() == name, ct))
        {
            throw new LookupNameTakenException(Kind, value.Name);
        }
    }
}
