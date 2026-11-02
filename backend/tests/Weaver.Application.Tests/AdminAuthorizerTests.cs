using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class AdminAuthorizerTests
{
    private WeaverDbContext _db = null!;
    private AdminAuthorizer _authorizer = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero)));
        _authorizer = new AdminAuthorizer(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void EnsureAdmin_AllowsAnActiveHumanAdmin()
    {
        var admin = TestUsers.Add(_db, "admin", isAdmin: true);

        Assert.DoesNotThrowAsync(() => _authorizer.EnsureAdminAsync(admin.Id));
    }

    [Test]
    public void EnsureAdmin_RefusesANonAdmin()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<AdminRequiredException>(() => _authorizer.EnsureAdminAsync(member.Id));
    }

    // Agents could be made admins before GrantAdmin refused them; such a row must still not
    // let the agent's key change admin data.
    [Test]
    public void EnsureAdmin_RefusesAnAgent_EvenOneFlaggedAsAdmin()
    {
        var agent = TestUsers.AddAgentFlaggedAsAdmin(_db, "Nightly triage");

        var ex = Assert.ThrowsAsync<AgentAdministrationException>(() => _authorizer.EnsureAdminAsync(agent.Id));

        Assert.That(ex!.Kind, Is.EqualTo(DomainErrorKind.Forbidden));
    }
}
