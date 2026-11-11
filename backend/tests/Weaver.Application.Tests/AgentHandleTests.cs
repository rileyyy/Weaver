using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>An admin giving agent accounts their <c>@</c> handles.</summary>
[TestFixture]
public class AgentHandleTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private AgentAccountService _agents = null!;
    private User _admin = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _caller = new FakeCallerContext(_admin.Id);
        var admins = new AdminAuthorizer(_db);
        var users = new UserAdministrationService(_db, admins, new CountingPasswordHasher(), _clock, _caller);
        _agents = new AgentAccountService(_db, admins, _caller, users, _clock);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_WithAHandle_GivesTheAgentIt()
    {
        var agent = await _agents.CreateAsync("Claude research", null, "claude-research");

        Assert.That(agent.Handle, Is.EqualTo("claude-research"));
    }

    [Test]
    public async Task SetHandleAsync_ChangesAndClearsTheHandle()
    {
        var agent = await _agents.CreateAsync("Claude research", null, null);

        var named = await _agents.SetHandleAsync(agent.Id, "claude-research");
        Assert.That(named.Handle, Is.EqualTo("claude-research"));

        var cleared = await _agents.SetHandleAsync(agent.Id, null);
        Assert.That(cleared.Handle, Is.Null);
    }

    [Test]
    public async Task SetHandleAsync_KeepingItsOwnHandle_IsAllowed()
    {
        var agent = await _agents.CreateAsync("Claude research", null, "claude-research");

        var same = await _agents.SetHandleAsync(agent.Id, "claude-research");

        Assert.That(same.Handle, Is.EqualTo("claude-research"));
    }

    [Test]
    public async Task AHandleAlreadyInUse_IsRefused()
    {
        await _agents.CreateAsync("Claude research", null, "claude");
        var other = await _agents.CreateAsync("Llama", null, null);

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<HandleTakenException>(() => _agents.CreateAsync("Claude again", null, "claude"));
            Assert.ThrowsAsync<HandleTakenException>(() => _agents.SetHandleAsync(other.Id, "claude"));
        });
    }

    [Test]
    public async Task AnInvalidHandle_IsRefused()
    {
        var agent = await _agents.CreateAsync("Claude research", null, null);

        Assert.ThrowsAsync<DomainValidationException>(() => _agents.SetHandleAsync(agent.Id, "Claude Research"));
    }

    [Test]
    public void SetHandleAsync_OnAHuman_IsRefused()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<NotAnAgentAccountException>(() => _agents.SetHandleAsync(member.Id, "member"));
    }

    [Test]
    public async Task SetHandleAsync_ByANonAdmin_IsRefused()
    {
        var agent = await _agents.CreateAsync("Claude research", null, null);
        _caller.Current = new Auth.Caller(TestUsers.Add(_db, "member").Id, null, null);

        Assert.ThrowsAsync<AdminRequiredException>(() => _agents.SetHandleAsync(agent.Id, "claude"));
    }
}
