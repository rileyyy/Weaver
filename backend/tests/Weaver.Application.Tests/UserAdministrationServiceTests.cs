using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Application.Tests;

[TestFixture]
public class UserAdministrationServiceTests
{
    private const string ValidPassword = "correct horse battery";

    private FixedTimeProvider _clock = null!;
    private CountingPasswordHasher _hasher = null!;
    private WeaverDbContext _db = null!;
    private UserAdministrationService _service = null!;
    private User _admin = null!;
    private User _member = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _hasher = new CountingPasswordHasher();
        _db = TestDatabase.Create(_clock);
        _service = new UserAdministrationService(_db, new AdminAuthorizer(_db), _hasher, _clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _member = TestUsers.Add(_db, "member");
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task ListAsync_IncludesDeactivatedUsers()
    {
        _member.Deactivate(_clock.GetUtcNow());
        await _db.SaveChangesAsync();

        var users = await _service.ListAsync(_admin.Id);

        Assert.That(users.Select(u => u.Username), Is.EquivalentTo(new[] { "admin", "member" }));
    }

    [Test]
    public void EveryOperation_ByANonAdmin_IsForbidden()
    {
        var id = _admin.Id;
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.ListAsync(_member.Id));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.CreateAsync(_member.Id, "eve", ValidPassword, true));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.RenameAsync(_member.Id, id, "eve"));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.ResetPasswordAsync(_member.Id, id, ValidPassword));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.UnlockAsync(_member.Id, id));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.SetAdminAsync(_member.Id, _member.Id, true));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.SetActiveAsync(_member.Id, id, false));
            Assert.ThrowsAsync<AdminRequiredException>(() => _service.DeleteAsync(_member.Id, id));
        });
    }

    [Test]
    public async Task CreateAsync_CreatesAHumanWithAHashedPassword()
    {
        var created = await _service.CreateAsync(_admin.Id, " Bob ", ValidPassword, isAdmin: true);

        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Username, Is.EqualTo("Bob"));
            Assert.That(stored.NormalizedUsername, Is.EqualTo("bob"));
            Assert.That(stored.IsAdmin, Is.True);
            Assert.That(stored.Kind, Is.EqualTo(UserKind.Human));
            Assert.That(stored.PasswordHash, Does.Not.Contain(ValidPassword));
        });
    }

    [Test]
    public void CreateAsync_WithInvalidCredentials_IsAValidationError()
    {
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<InvalidUsernameException>(() => _service.CreateAsync(_admin.Id, "a", ValidPassword, false));
            Assert.ThrowsAsync<InvalidPasswordException>(() => _service.CreateAsync(_admin.Id, "bob", "short", false));
        });
    }

    [Test]
    public void CreateAsync_WithATakenUsernameInAnyCase_IsAConflict()
    {
        Assert.ThrowsAsync<UsernameTakenException>(() => _service.CreateAsync(_admin.Id, "MEMBER", ValidPassword, false));
    }

    [Test]
    public async Task RenameAsync_ChangesTheUsername()
    {
        await _service.RenameAsync(_admin.Id, _member.Id, "Morgan");

        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _member.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Username, Is.EqualTo("Morgan"));
            Assert.That(stored.NormalizedUsername, Is.EqualTo("morgan"));
        });
    }

    [Test]
    public async Task RenameAsync_ToItsOwnNameInADifferentCase_IsAllowed()
    {
        var renamed = await _service.RenameAsync(_admin.Id, _member.Id, "Member");

        Assert.That(renamed.Username, Is.EqualTo("Member"));
    }

    [Test]
    public void RenameAsync_ToAnotherUsersName_IsAConflict()
    {
        Assert.ThrowsAsync<UsernameTakenException>(() => _service.RenameAsync(_admin.Id, _member.Id, "Admin"));
    }

    [Test]
    public void RenameAsync_ForAMissingUser_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _service.RenameAsync(_admin.Id, Guid.NewGuid(), "someone"));
    }

    [Test]
    public async Task ResetPasswordAsync_SetsANewHashClearsLockoutAndRevokesSessions()
    {
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            _member.RecordFailedLogin(_clock.GetUtcNow());
        }
        var token = AddRefreshToken(_member);
        var oldHash = _member.PasswordHash;

        await _service.ResetPasswordAsync(_admin.Id, _member.Id, ValidPassword);

        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == _member.Id);
        var storedToken = await _db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.PasswordHash, Is.Not.EqualTo(oldHash));
            Assert.That(stored.IsLockedOutAt(_clock.GetUtcNow()), Is.False);
            Assert.That(storedToken.RevokedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public void ResetPasswordAsync_WithATooShortPassword_IsAValidationError()
    {
        Assert.ThrowsAsync<InvalidPasswordException>(() => _service.ResetPasswordAsync(_admin.Id, _member.Id, "short"));
    }

    [Test]
    public async Task UnlockAsync_ClearsTheLockout()
    {
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            _member.RecordFailedLogin(_clock.GetUtcNow());
        }
        await _db.SaveChangesAsync();

        var unlocked = await _service.UnlockAsync(_admin.Id, _member.Id);

        Assert.Multiple(() =>
        {
            Assert.That(unlocked.IsLockedOutAt(_clock.GetUtcNow()), Is.False);
            Assert.That(unlocked.FailedLoginAttempts, Is.Zero);
        });
    }

    [Test]
    public async Task SetAdminAsync_GrantsAndRevokes()
    {
        await _service.SetAdminAsync(_admin.Id, _member.Id, true);
        Assert.That((await Reload(_member)).IsAdmin, Is.True);

        await _service.SetAdminAsync(_admin.Id, _member.Id, false);
        Assert.That((await Reload(_member)).IsAdmin, Is.False);
    }

    [Test]
    public async Task SetAdminAsync_RevokingTheLastActiveAdmin_IsAConflict()
    {
        Assert.ThrowsAsync<LastAdminException>(() => _service.SetAdminAsync(_admin.Id, _admin.Id, false));
        Assert.That((await Reload(_admin)).IsAdmin, Is.True);
    }

    [Test]
    public async Task SetAdminAsync_RevokingYourOwnAdmin_IsAllowedWhenAnotherAdminRemains()
    {
        TestUsers.Add(_db, "second", isAdmin: true);

        await _service.SetAdminAsync(_admin.Id, _admin.Id, false);

        Assert.That((await Reload(_admin)).IsAdmin, Is.False);
    }

    [Test]
    public async Task SetAdminAsync_ADeactivatedAdminDoesNotCountAsAnotherAdmin()
    {
        var inactive = TestUsers.Add(_db, "inactive", isAdmin: true);
        inactive.Deactivate(_clock.GetUtcNow());
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<LastAdminException>(() => _service.SetAdminAsync(_admin.Id, _admin.Id, false));
    }

    [Test]
    public async Task SetActiveAsync_Deactivating_UnassignsWorkAndRevokesSessions()
    {
        var item = WorkItem.Create("Assigned", null, null, StatusConfiguration.ToDoId, 1);
        item.AssignTo(_member.Id);
        _db.WorkItems.Add(item);
        var token = AddRefreshToken(_member);

        await _service.SetActiveAsync(_admin.Id, _member.Id, false);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await Reload(_member)).IsActive, Is.False);
            Assert.That((await _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id)).AssignedToUserId, Is.Null);
            Assert.That((await _db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).RevokedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task SetActiveAsync_Reactivating_RestoresTheAccount()
    {
        await _service.SetActiveAsync(_admin.Id, _member.Id, false);

        await _service.SetActiveAsync(_admin.Id, _member.Id, true);

        Assert.That((await Reload(_member)).IsActive, Is.True);
    }

    [Test]
    public void SetActiveAsync_DeactivatingYourself_IsAConflict()
    {
        TestUsers.Add(_db, "second", isAdmin: true);

        Assert.ThrowsAsync<OwnAccountChangeException>(() => _service.SetActiveAsync(_admin.Id, _admin.Id, false));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheUserTheirTokensAndAssignments()
    {
        var item = WorkItem.Create("Assigned", null, null, StatusConfiguration.ToDoId, 1);
        item.AssignTo(_member.Id);
        _db.WorkItems.Add(item);
        AddRefreshToken(_member);

        await _service.DeleteAsync(_admin.Id, _member.Id);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _db.Users.AnyAsync(u => u.Id == _member.Id), Is.False);
            Assert.That(await _db.RefreshTokens.AnyAsync(t => t.UserId == _member.Id), Is.False);
            Assert.That((await _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id)).AssignedToUserId, Is.Null);
        });
    }

    [Test]
    public async Task DeleteAsync_AUserWithComments_IsAConflictAndDeletesNothing()
    {
        var item = WorkItem.Create("Commented", null, null, StatusConfiguration.ToDoId, 1);
        _db.WorkItems.Add(item);
        _db.Comments.Add(Comment.Create(item.Id, _member.Id, "Hello"));
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<UserHasCommentsException>(() => _service.DeleteAsync(_admin.Id, _member.Id));
        Assert.That(await _db.Users.AnyAsync(u => u.Id == _member.Id), Is.True);
    }

    [Test]
    public void DeleteAsync_Yourself_IsAConflict()
    {
        Assert.ThrowsAsync<OwnAccountChangeException>(() => _service.DeleteAsync(_admin.Id, _admin.Id));
    }

    [Test]
    public void DeleteAsync_AMissingUser_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _service.DeleteAsync(_admin.Id, Guid.NewGuid()));
    }

    private RefreshToken AddRefreshToken(User user)
    {
        var token = RefreshToken.Issue(user.Id, Guid.NewGuid().ToString("N"), _clock.GetUtcNow().AddDays(1));
        _db.RefreshTokens.Add(token);
        _db.SaveChanges();
        return token;
    }

    private Task<User> Reload(User user) => _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
}
