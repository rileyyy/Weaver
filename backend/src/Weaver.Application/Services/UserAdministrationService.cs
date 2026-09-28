using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class UserAdministrationService : IUserAdministrationService
{
    private readonly IWeaverDbContext _db;
    private readonly IAdminAuthorizer _admins;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _clock;

    public UserAdministrationService(
        IWeaverDbContext db,
        IAdminAuthorizer admins,
        IPasswordHasher<User> passwordHasher,
        TimeProvider clock)
    {
        _db = db;
        _admins = admins;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<IReadOnlyList<User>> ListAsync(Guid actorUserId, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);
        return await _db.Users.AsNoTracking().OrderBy(u => u.NormalizedUsername).ToListAsync(ct);
    }

    public async Task<User> CreateAsync(
        Guid actorUserId,
        string username,
        string password,
        bool isAdmin,
        CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var (trimmed, normalized) = UserCredentials.NormalizeUsername(username);
        UserCredentials.ValidatePassword(password);
        await EnsureUsernameFreeAsync(trimmed, normalized, exceptUserId: null, ct);

        var user = User.CreateHuman(trimmed, normalized, isAdmin);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
        _db.Users.Add(user);

        await SaveUsernameChangeAsync(trimmed, ct);
        return user;
    }

    public async Task<User> RenameAsync(Guid actorUserId, Guid userId, string username, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var (trimmed, normalized) = UserCredentials.NormalizeUsername(username);
        var user = await FindAsync(userId, ct);
        await EnsureUsernameFreeAsync(trimmed, normalized, exceptUserId: userId, ct);

        user.Rename(trimmed, normalized);
        await SaveUsernameChangeAsync(trimmed, ct);
        return user;
    }

    public async Task<User> ResetPasswordAsync(
        Guid actorUserId,
        Guid userId,
        string password,
        CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        UserCredentials.ValidatePassword(password);
        var user = await FindAsync(userId, ct);

        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
        user.ClearLockout();
        await RevokeSessionsAsync(userId, ct);

        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> UnlockAsync(Guid actorUserId, Guid userId, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var user = await FindAsync(userId, ct);
        user.ClearLockout();

        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> SetAdminAsync(Guid actorUserId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var user = await FindAsync(userId, ct);
        if (isAdmin)
        {
            user.GrantAdmin();
        }
        else
        {
            await EnsureAnotherActiveAdminAsync(user, ct);
            user.RevokeAdmin();
        }

        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> SetActiveAsync(Guid actorUserId, Guid userId, bool isActive, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);

        var user = await FindAsync(userId, ct);
        if (isActive)
        {
            user.Reactivate();
        }
        else
        {
            EnsureNotOwnAccount(actorUserId, userId);
            await EnsureAnotherActiveAdminAsync(user, ct);
            user.Deactivate(_clock.GetUtcNow());
            await UnassignWorkItemsAsync(userId, ct);
            await RevokeSessionsAsync(userId, ct);
        }

        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task DeleteAsync(Guid actorUserId, Guid userId, CancellationToken ct = default)
    {
        await _admins.EnsureAdminAsync(actorUserId, ct);
        EnsureNotOwnAccount(actorUserId, userId);

        var user = await FindAsync(userId, ct);
        await EnsureAnotherActiveAdminAsync(user, ct);

        var commentCount = await _db.Comments.CountAsync(c => c.AuthorUserId == userId, ct);
        if (commentCount > 0)
        {
            throw new UserHasCommentsException(user.Username, commentCount);
        }

        // The database would null the assignments and cascade the tokens by itself; doing
        // both here keeps the tracked entities (and the InMemory tests) consistent with it.
        await UnassignWorkItemsAsync(userId, ct);
        _db.RefreshTokens.RemoveRange(await _db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct));
        _db.Users.Remove(user);

        await _db.SaveChangesAsync(ct);
    }

    private async Task<User> FindAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.FindAsync([userId], ct)
        ?? throw new EntityNotFoundException(nameof(User), userId);

    private static void EnsureNotOwnAccount(Guid actorUserId, Guid userId)
    {
        if (actorUserId == userId)
        {
            throw new OwnAccountChangeException();
        }
    }

    /// <summary>
    /// Removing, deactivating or demoting an active admin must leave at least one other,
    /// or nobody could manage accounts again without editing the database.
    /// </summary>
    private async Task EnsureAnotherActiveAdminAsync(User user, CancellationToken ct)
    {
        if (!user.IsAdmin || !user.IsActive)
        {
            return;
        }

        var hasAnother = await _db.Users.AnyAsync(
            u => u.Id != user.Id && u.IsAdmin && u.DeactivatedAtUtc == null,
            ct);
        if (!hasAnother)
        {
            throw new LastAdminException();
        }
    }

    private async Task EnsureUsernameFreeAsync(string username, string normalized, Guid? exceptUserId, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.NormalizedUsername == normalized && u.Id != exceptUserId, ct))
        {
            throw new UsernameTakenException(username);
        }
    }

    private async Task SaveUsernameChangeAsync(string username, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
        {
            // Someone else took the name between the check and this save.
            throw new UsernameTakenException(username, ex);
        }
    }

    private async Task UnassignWorkItemsAsync(Guid userId, CancellationToken ct)
    {
        var assigned = await _db.WorkItems.Where(w => w.AssignedToUserId == userId).ToListAsync(ct);
        foreach (var item in assigned)
        {
            item.AssignTo(null);
        }
    }

    /// <summary>
    /// Refresh tokens only; an access token already issued stays valid until it expires
    /// (at most <c>JwtOptions.AccessTokenLifetime</c>), as after a normal logout.
    /// </summary>
    private async Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }
}
