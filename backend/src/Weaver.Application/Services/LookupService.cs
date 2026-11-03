using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// The admin operations every lookup shares: listing, adding at the end, editing, reordering
/// and deleting with a replacement. Each works within a value's siblings: every value of a
/// global lookup, or the values of one Space. Subclasses say which table they use and how work
/// items are moved off a deleted value.
/// </summary>
public abstract class LookupService<T>(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller) : ILookupService<T> where T : LookupValue
{
    private readonly IAdminAuthorizer _admins = admins;

    protected IWeaverDbContext Db { get; } = db;

    /// <summary>Moving work items off a deleted value changes them, so each move is recorded.</summary>
    private protected WorkItemHistoryRecorder History { get; } = new WorkItemHistoryRecorder(db, caller);

    protected abstract DbSet<T> Values { get; }

    /// <summary>The kind's display name, for error messages ("Status", "Priority").</summary>
    protected abstract string Kind { get; }

    /// <summary>How many values must remain among siblings; deleting below it is refused.</summary>
    protected virtual int MinimumCount => 0;

    /// <summary>The values <paramref name="value"/> shares names, order and replacements with.</summary>
    protected abstract IQueryable<T> SiblingsOf(T value);

    public async Task DeleteAsync(Guid actorUserId, Guid id, Guid? replacementId, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var value = await Values.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(Kind, id);

        if (replacementId == id)
        {
            throw new DomainValidationException($"A {Kind.ToLowerInvariant()} can't be replaced by itself.");
        }

        var siblings = SiblingsOf(value);
        if (replacementId is { } replacement && !await siblings.AnyAsync(v => v.Id == replacement, ct))
        {
            throw new EntityNotFoundException(Kind, replacement);
        }

        if (await siblings.CountAsync(ct) <= MinimumCount)
        {
            throw new LastLookupValueException(Kind);
        }

        await MoveWorkItemsAsync(value, replacementId, ct);
        Values.Remove(value);
        await Db.SaveChangesAsync(ct);
    }

    protected static async Task<IReadOnlyList<T>> ListAsync(IQueryable<T> siblings, CancellationToken ct) =>
        await siblings.AsNoTracking().OrderBy(v => v.Order).ToListAsync(ct);

    protected async Task<IReadOnlyList<T>> ReorderWithinAsync(
        Guid actorUserId,
        IQueryable<T> siblings,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var values = await siblings.ToDictionaryAsync(v => v.Id, ct);
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

    /// <summary>Appends a new value after the current last of <paramref name="siblings"/>.</summary>
    protected async Task<T> AddWithinAsync(
        Guid actorUserId,
        IQueryable<T> siblings,
        Func<int, T> create,
        CancellationToken ct)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var lastOrder = await siblings.MaxAsync(v => (int?)v.Order, ct);
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

    /// <summary>Two sibling values with the same name would be indistinguishable in every picker.</summary>
    private async Task EnsureNameFreeAsync(T value, CancellationToken ct)
    {
        var name = value.Name.ToLower();
        if (await SiblingsOf(value).AnyAsync(v => v.Id != value.Id && v.Name.ToLower() == name, ct))
        {
            throw new LookupNameTakenException(Kind, value.Name);
        }
    }
}
