using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Services;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class AuthServiceTests
{
    private const string ValidPassword = "correct horse battery";

    private WeaverDbContext _db = null!;
    private AuthService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();

        var jwtOptions = Options.Create(new JwtOptions { SigningKey = "test-only-signing-key-at-least-32-bytes-long" });
        _service = new AuthService(_db, new JwtTokenService(jwtOptions), new PasswordHasher<User>());
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task RegisterAsync_CreatesUserAndReturnsTokens()
    {
        var result = await _service.RegisterAsync("alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
        Assert.That(result.User.Kind, Is.EqualTo(UserKind.Human));
        Assert.That(result.AccessToken.Value, Is.Not.Empty);
        Assert.That(result.RefreshToken, Is.Not.Empty);
    }

    [Test]
    public async Task RegisterAsync_HashesThePassword_NeverStoresItInPlainText()
    {
        var result = await _service.RegisterAsync("alice", ValidPassword);

        Assert.That(result.User.PasswordHash, Does.Not.Contain(ValidPassword));
    }

    [Test]
    public async Task RegisterAsync_WithDuplicateUsername_ThrowsRegardlessOfCase()
    {
        await _service.RegisterAsync("alice", ValidPassword);

        Assert.ThrowsAsync<UsernameTakenException>(() => _service.RegisterAsync("ALICE", ValidPassword));
    }

    [Test]
    public void RegisterAsync_WithShortPassword_ThrowsInvalidPasswordException()
    {
        Assert.ThrowsAsync<InvalidPasswordException>(() => _service.RegisterAsync("alice", "short"));
    }

    [Test]
    public void RegisterAsync_WithInvalidUsername_ThrowsInvalidUsernameException()
    {
        Assert.ThrowsAsync<InvalidUsernameException>(() => _service.RegisterAsync("a b!", ValidPassword));
    }

    [Test]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokens()
    {
        await _service.RegisterAsync("alice", ValidPassword);

        var result = await _service.LoginAsync("alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
    }

    [Test]
    public async Task LoginAsync_IsCaseInsensitiveOnUsername()
    {
        await _service.RegisterAsync("alice", ValidPassword);

        var result = await _service.LoginAsync("Alice", ValidPassword);

        Assert.That(result.User.Username, Is.EqualTo("alice"));
    }

    [Test]
    public void LoginAsync_WithUnknownUsername_ThrowsInvalidCredentialsException()
    {
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("nobody", ValidPassword));
    }

    [Test]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentialsException()
    {
        await _service.RegisterAsync("alice", ValidPassword);

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password entirely"));
    }

    [Test]
    public async Task LoginAsync_AfterFiveFailedAttempts_LocksTheAccountEvenWithTheCorrectPassword()
    {
        await _service.RegisterAsync("alice", ValidPassword);

        for (var i = 0; i < 5; i++)
        {
            Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password entirely"));
        }

        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", ValidPassword));
    }

    [Test]
    public async Task LoginAsync_OnSuccess_ResetsThePriorFailedAttemptCounter()
    {
        await _service.RegisterAsync("alice", ValidPassword);
        Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync("alice", "wrong password entirely"));

        await _service.LoginAsync("alice", ValidPassword);

        var user = await _db.Users.SingleAsync();
        Assert.That(user.FailedLoginAttempts, Is.EqualTo(0));
    }

    [Test]
    public async Task RefreshAsync_WithAValidToken_ReturnsANewTokenPair()
    {
        var initial = await _service.RegisterAsync("alice", ValidPassword);

        var refreshed = await _service.RefreshAsync(initial.RefreshToken);

        Assert.That(refreshed.RefreshToken, Is.Not.EqualTo(initial.RefreshToken));
        Assert.That(refreshed.User.Id, Is.EqualTo(initial.User.Id));
    }

    [Test]
    public async Task RefreshAsync_RevokesTheTokenItWasCalledWith_SoItCannotBeReused()
    {
        var initial = await _service.RegisterAsync("alice", ValidPassword);
        await _service.RefreshAsync(initial.RefreshToken);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(initial.RefreshToken));
    }

    [Test]
    public void RefreshAsync_WithAnUnknownToken_ThrowsInvalidRefreshTokenException()
    {
        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync("not-a-real-token"));
    }

    [Test]
    public async Task LogoutAsync_RevokesTheToken_SoItCanNoLongerBeRefreshed()
    {
        var initial = await _service.RegisterAsync("alice", ValidPassword);

        await _service.LogoutAsync(initial.RefreshToken);

        Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(initial.RefreshToken));
    }

    [Test]
    public void LogoutAsync_WithAnUnknownToken_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(() => _service.LogoutAsync("not-a-real-token"));
    }
}
