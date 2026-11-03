using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class SpaceService(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller) : ISpaceService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IAdminAuthorizer _admins = admins;
    private readonly ICallerContext _caller = caller;

    public async Task<IReadOnlyList<Space>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Spaces.AsNoTracking().OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(ct);

    public Task<Space?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Spaces.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Space> CreateAsync(string name, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);

        var space = Space.Create(name);
        await EnsureNameFreeAsync(space.Name, exceptId: null, ct);

        _db.Spaces.Add(space);
        Record(space.Id, SpaceHistoryField.Created, null, space.Name);
        await SaveAsync(space.Name, ct);
        return space;
    }

    public async Task<Space> RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);

        var space = await _db.Spaces.FindAsync([id], ct) ?? throw new EntityNotFoundException(nameof(Space), id);
        var changes = space.Rename(name);
        if (changes.Count == 0)
        {
            return space;
        }

        await EnsureNameFreeAsync(space.Name, exceptId: id, ct);
        foreach (var change in changes)
        {
            Record(id, change.Field, change.OldValue, change.NewValue);
        }

        await SaveAsync(space.Name, ct);
        return space;
    }

    private Task EnsureAdminAsync(CancellationToken ct) => _admins.EnsureAdminAsync(_caller.Current.AccountId, ct);

    /// <summary>
    /// "Home" and "home" would be indistinguishable in a switcher, so names are compared
    /// ignoring case here; the unique index only catches an exact duplicate saved concurrently.
    /// </summary>
    private async Task EnsureNameFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lowered = name.ToLower();
        if (await _db.Spaces.AnyAsync(s => s.Id != exceptId && s.Name.ToLower() == lowered, ct))
        {
            throw new SpaceNameTakenException(name);
        }
    }

    private async Task SaveAsync(string name, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw new SpaceNameTakenException(name, ex);
        }
    }

    private void Record(Guid spaceId, string field, object? oldValue, object? newValue) =>
        _db.SpaceHistory.Add(SpaceHistoryEntry.Record(
            spaceId, _caller.Current.HistoryActor, field, HistoryJson.Serialize(oldValue), HistoryJson.Serialize(newValue)));
}
