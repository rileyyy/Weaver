using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class FirstAdminBootstrapperTests
{
    private const string ValidPassword = "correct horse battery";

    private FixedTimeProvider _clock = null!;
    private CountingPasswordHasher _hasher = null!;
    private WeaverDbContext _db = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _hasher = new CountingPasswordHasher();
        _db = TestDatabase.Create(_clock);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task EnsureFirstAdminAsync_OnAnEmptyDatabase_CreatesTheConfiguredAdmin()
    {
        var result = await Bootstrapper("Alice", ValidPassword).EnsureFirstAdminAsync();

        var user = _db.Users.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(FirstAdminBootstrapResult.Created));
            Assert.That(user.Username, Is.EqualTo("Alice"));
            Assert.That(user.IsAdmin, Is.True);
            Assert.That(user.IsActive, Is.True);
        });
    }

    [Test]
    public async Task EnsureFirstAdminAsync_WhenAnyAccountExists_CreatesNothing()
    {
        TestUsers.Add(_db, "bob");

        var result = await Bootstrapper("alice", ValidPassword).EnsureFirstAdminAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(FirstAdminBootstrapResult.AccountsAlreadyExist));
            Assert.That(_db.Users.Select(u => u.Username), Is.EquivalentTo(new[] { "bob" }));
        });
    }

    [TestCase(null, ValidPassword)]
    [TestCase("alice", null)]
    [TestCase(" ", ValidPassword)]
    [TestCase("alice", "")]
    public async Task EnsureFirstAdminAsync_WithoutAUsernameAndPassword_CreatesNothing(string? username, string? password)
    {
        var result = await Bootstrapper(username, password).EnsureFirstAdminAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(FirstAdminBootstrapResult.NotConfigured));
            Assert.That(_db.Users, Is.Empty);
        });
    }

    [Test]
    public void EnsureFirstAdminAsync_WithAShortPassword_ThrowsInvalidPasswordException()
    {
        Assert.ThrowsAsync<InvalidPasswordException>(() => Bootstrapper("alice", "short").EnsureFirstAdminAsync());
        Assert.That(_db.Users, Is.Empty);
    }

    [Test]
    public async Task EnsureFirstAdminAsync_TheAdminCanSignInWithTheConfiguredPassword()
    {
        await Bootstrapper("alice", ValidPassword).EnsureFirstAdminAsync();

        var user = _db.Users.Single();
        Assert.Multiple(() =>
        {
            Assert.That(user.PasswordHash, Does.Not.Contain(ValidPassword));
            Assert.That(
                _hasher.VerifyHashedPassword(user, user.PasswordHash, ValidPassword),
                Is.Not.EqualTo(PasswordVerificationResult.Failed));
        });
    }

    private FirstAdminBootstrapper Bootstrapper(string? username, string? password) =>
        new(_db, _hasher, Options.Create(new FirstAdminOptions { Username = username, Password = password }));
}
