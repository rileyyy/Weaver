using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Auth;

namespace Weaver.Application.Tests;

[TestFixture]
public class AuthServiceTests
{
    private const string ValidPassword = "correct horse battery";

    private FixedTimeProvider _clock = null!;
    private CountingPasswordHasher _hasher = null!;
    private WeaverDbContext _db = null!;
    private AuthService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _hasher = new CountingPasswordHasher();
        _db = TestDatabase.Create(_clock);

        var jwtOptions = Options.Create(new JwtOptions { SigningKey = "test-only-signing-key-at-least-32-bytes-long" });
        _service = new AuthService(_db, new JwtTokenService(jwtOptions, _clock), _hasher, jwtOptions, _clock);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task LoginAsync_ForADeactivatedUser_FailsLikeAWrongPassword()
    {
        var user = AddUser("alice");
        user.Deactivate(_clock.GetUtcNow());
        await _db.SaveChangesAsync();
        var verificationsBefore = _hasher.Verifications;

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", ValidPassword));
        Assert.That(_hasher.Verifications, Is.EqualTo(verificationsBefore + 1), "the same hashing cost as a real check");
    }

    [Test]
    public async Task RefreshAsync_ForADeactivatedUser_IsRejected()
    {
        var session = await SignInNewUserAsync("alice");
        session.User.Deactivate(_clock.GetUtcNow());
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(session.RefreshToken));
    }

    [Test]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokens()
    {
        AddUser("alice");

        var result = await _service.LoginAsync("alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
    }

    [Test]
    public async Task LoginAsync_IsCaseInsensitiveOnUsername()
    {
        AddUser("alice");

        var result = await _service.LoginAsync("Alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
    }

    [Test]
    public void LoginAsync_WithUnknownUsername_ThrowsInvalidCredentialsException()
    {
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("nobody", ValidPassword));
    }

    [Test]
    public void LoginAsync_WithWrongPassword_ThrowsInvalidCredentialsException()
    {
        AddUser("alice");

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password entirely"));
    }

    [Test]
    public async Task LoginAsync_OnceTheLockoutHasExpired_AcceptsTheCorrectPassword()
    {
        AddUser("alice");
        for (var i = 0; i < 5; i++)
        {
            Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password!"));
        }

        _clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        var result = await _service.LoginAsync("alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
    }

    [Test]
    public async Task LoginAsync_AfterALockoutExpires_TheFirstMistakeDoesNotRelock()
    {
        AddUser("alice");
        for (var i = 0; i < 5; i++)
        {
            Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password!"));
        }

        _clock.Advance(TimeSpan.FromMinutes(16));
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password!"));
        var result = await _service.LoginAsync("alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
    }

    [Test]
    public void LoginAsync_ForAnUnknownUser_StillVerifiesAPassword()
    {
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("nobody", ValidPassword));

        Assert.That(_hasher.Verifications, Is.EqualTo(1));
    }

    [Test]
    public void LoginAsync_ForALockedOutUser_StillVerifiesAPassword()
    {
        AddUser("alice");
        for (var i = 0; i < 5; i++)
        {
            Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password!"));
        }

        var before = _hasher.Verifications;
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", ValidPassword));

        Assert.That(_hasher.Verifications, Is.EqualTo(before + 1));
    }

    [Test]
    public void LoginAsync_AfterFiveFailedAttempts_LocksTheAccountEvenWithTheCorrectPassword()
    {
        AddUser("alice");

        for (var i = 0; i < 5; i++)
        {
            Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password entirely"));
        }

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", ValidPassword));
    }

    [Test]
    public async Task LoginAsync_OnSuccess_ResetsThePriorFailedAttemptCounter()
    {
        AddUser("alice");
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password entirely"));

        await _service.LoginAsync("alice", ValidPassword);

        var user = await _db.Users.SingleAsync();
        Assert.That(user.FailedLoginAttempts, Is.EqualTo(0));
    }

    [Test]
    public async Task RefreshAsync_WithAValidToken_ReturnsANewTokenPair()
    {
        var initial = await SignInNewUserAsync("alice");

        var refreshed = await _service.RefreshAsync(initial.RefreshToken);

        Assert.That(refreshed.RefreshToken, Is.Not.EqualTo(initial.RefreshToken));
        Assert.That(refreshed.User.Id, Is.EqualTo(initial.User.Id));
    }

    [Test]
    public async Task RefreshAsync_RevokesTheTokenItWasCalledWith_SoItCannotBeReused()
    {
        var initial = await SignInNewUserAsync("alice");
        await _service.RefreshAsync(initial.RefreshToken);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(initial.RefreshToken));
    }

    [Test]
    public void RefreshAsync_WithAnUnknownToken_ThrowsInvalidRefreshTokenException()
    {
        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync("not-a-real-token"));
    }

    [Test]
    public async Task RefreshAsync_ReusingARotatedTokenAfterTheGracePeriod_RevokesEveryTokenIssuedFromIt()
    {
        var initial = await SignInNewUserAsync("alice");
        var second = await _service.RefreshAsync(initial.RefreshToken);
        var third = await _service.RefreshAsync(second.RefreshToken);
        await BackdateRevocationAsync(initial.RefreshToken, TimeSpan.FromMinutes(5));

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(initial.RefreshToken));

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(third.RefreshToken));
    }

    [Test]
    public async Task RefreshAsync_ReusingARotatedTokenWithinTheGracePeriod_IsRejectedButKeepsTheNewToken()
    {
        var initial = await SignInNewUserAsync("alice");
        var rotated = await _service.RefreshAsync(initial.RefreshToken);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(initial.RefreshToken));

        var refreshed = await _service.RefreshAsync(rotated.RefreshToken);
        Assert.That(refreshed.RefreshToken, Is.Not.Empty);
    }

    [Test]
    public async Task RefreshAsync_ReuseInOneSession_DoesNotRevokeTheUsersOtherSessions()
    {
        var sessionA = await SignInNewUserAsync("alice");
        var sessionB = await _service.LoginAsync("alice", ValidPassword);
        await _service.RefreshAsync(sessionA.RefreshToken);
        await BackdateRevocationAsync(sessionA.RefreshToken, TimeSpan.FromMinutes(5));

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(sessionA.RefreshToken));

        var refreshedB = await _service.RefreshAsync(sessionB.RefreshToken);
        Assert.That(refreshedB.RefreshToken, Is.Not.Empty);
    }

    [Test]
    public async Task RefreshAsync_WithALoggedOutToken_DoesNotRevokeAnythingElse()
    {
        var sessionA = await SignInNewUserAsync("alice");
        var sessionB = await _service.LoginAsync("alice", ValidPassword);
        await _service.LogoutAsync(sessionA.RefreshToken);
        await BackdateRevocationAsync(sessionA.RefreshToken, TimeSpan.FromMinutes(5));

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(sessionA.RefreshToken));

        Assert.That((await _service.RefreshAsync(sessionB.RefreshToken)).RefreshToken, Is.Not.Empty);
    }

    [Test]
    public async Task IssuedRefreshTokens_UseTheConfiguredLifetime()
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "test-only-signing-key-at-least-32-bytes-long",
            RefreshTokenLifetime = TimeSpan.FromDays(2),
        });
        var service = new AuthService(_db, new JwtTokenService(jwtOptions, _clock), new PasswordHasher<User>(), jwtOptions, _clock);

        AddUser("alice");
        await service.LoginAsync("alice", ValidPassword);

        var stored = await _db.RefreshTokens.SingleAsync();
        Assert.That(stored.ExpiresAtUtc - stored.CreatedAtUtc, Is.EqualTo(TimeSpan.FromDays(2)));
    }

    [Test]
    public async Task LoginAsync_RemovesTheUsersExpiredRefreshTokens_ButKeepsOtherUsersTokens()
    {
        var alice = await SignInNewUserAsync("alice");
        var bob = await SignInNewUserAsync("bob");
        await ExpireAllRefreshTokensAsync();

        await _service.LoginAsync("alice", ValidPassword);

        var remainingUserIds = await _db.RefreshTokens.Select(t => t.UserId).ToListAsync();
        Assert.That(remainingUserIds.Count(id => id == alice.User.Id), Is.EqualTo(1));
        Assert.That(remainingUserIds.Count(id => id == bob.User.Id), Is.EqualTo(1));
    }

    // Before SetPasswordHash refused agents, an admin could give one a password.
    [Test]
    public async Task LoginAsync_ForAnAgentWithAStoredPassword_FailsLikeAWrongPassword()
    {
        var agent = User.CreateAgent("nightly", null);
        _db.Users.Add(agent);
        _db.Entry(agent).Property(u => u.PasswordHash).CurrentValue = _hasher.HashPassword(agent, ValidPassword);
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("nightly", ValidPassword));
    }

    private User AddUser(string username)
    {
        var (trimmed, normalized) = UserCredentials.NormalizeUsername(username);
        var user = User.CreateHuman(trimmed, normalized);
        user.SetPasswordHash(_hasher.HashPassword(user, ValidPassword));
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private Task<AuthResult> SignInNewUserAsync(string username)
    {
        AddUser(username);
        return _service.LoginAsync(username, ValidPassword);
    }

    private async Task BackdateRevocationAsync(string refreshToken, TimeSpan ago)
    {
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));
        var stored = await _db.RefreshTokens.SingleAsync(t => t.TokenHash == tokenHash);
        _db.Entry(stored).Property(t => t.RevokedAtUtc).CurrentValue = _clock.GetUtcNow() - ago;
        await _db.SaveChangesAsync();
    }

    private async Task ExpireAllRefreshTokensAsync()
    {
        foreach (var token in await _db.RefreshTokens.ToListAsync())
        {
            _db.Entry(token).Property(t => t.ExpiresAtUtc).CurrentValue = _clock.GetUtcNow().AddMinutes(-1);
        }
        await _db.SaveChangesAsync();
    }

    [Test]
    public async Task LogoutAsync_RevokesTheToken_SoItCanNoLongerBeRefreshed()
    {
        var initial = await SignInNewUserAsync("alice");

        await _service.LogoutAsync(initial.RefreshToken);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(initial.RefreshToken));
    }

    [Test]
    public void LogoutAsync_WithAnUnknownToken_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(() => _service.LogoutAsync("not-a-real-token"));
    }
}
