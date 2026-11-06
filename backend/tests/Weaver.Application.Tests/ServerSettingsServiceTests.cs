using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class ServerSettingsServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private ServerSettingsService _service = null!;
    private User _admin = null!;
    private User _member = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _service = new ServerSettingsService(_db, new AdminAuthorizer(_db));
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _member = TestUsers.Add(_db, "member");
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAsync_OnAFreshServer_UsesUtc()
    {
        var settings = await _service.GetAsync();

        Assert.That(settings.TimeZoneId, Is.EqualTo("UTC"));
    }

    [Test]
    public async Task SetTimeZoneAsync_ByAnAdmin_IsStored()
    {
        await _service.SetTimeZoneAsync(_admin.Id, "Europe/Berlin");

        var settings = await _service.GetAsync();
        Assert.That(settings.TimeZoneId, Is.EqualTo("Europe/Berlin"));
    }

    [Test]
    public async Task SetTimeZoneAsync_TrimsTheId()
    {
        var settings = await _service.SetTimeZoneAsync(_admin.Id, "  America/New_York ");

        Assert.That(settings.TimeZoneId, Is.EqualTo("America/New_York"));
    }

    [Test]
    public async Task SetTimeZoneAsync_ByANonAdmin_IsForbiddenAndChangesNothing()
    {
        Assert.ThrowsAsync<AdminRequiredException>(() => _service.SetTimeZoneAsync(_member.Id, "Europe/Berlin"));

        var settings = await _service.GetAsync();
        Assert.That(settings.TimeZoneId, Is.EqualTo("UTC"));
    }

    [Test]
    public async Task SetTimeZoneAsync_ByADeactivatedAdmin_IsForbidden()
    {
        _admin.Deactivate(_clock.GetUtcNow());
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<AdminRequiredException>(() => _service.SetTimeZoneAsync(_admin.Id, "Europe/Berlin"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Mars/Olympus_Mons")]
    [TestCase("Local")]
    [TestCase("+02:00")]
    public async Task SetTimeZoneAsync_WithAnUnknownId_IsAValidationErrorAndChangesNothing(string timeZoneId)
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _service.SetTimeZoneAsync(_admin.Id, timeZoneId));

        var settings = await _service.GetAsync();
        Assert.That(settings.TimeZoneId, Is.EqualTo("UTC"));
    }

    // Windows ids resolve on a Windows host but not in the Linux container, so accepting one
    // would store a setting the production server can't read.
    [Test]
    public void SetTimeZoneAsync_WithAWindowsId_IsAValidationError()
    {
        Assert.ThrowsAsync<DomainValidationException>(
            () => _service.SetTimeZoneAsync(_admin.Id, "W. Europe Standard Time"));
    }

    [Test]
    public async Task GetAsync_OnAFreshServer_KeepsTrashForThirtyDays()
    {
        var settings = await _service.GetAsync();

        Assert.That(settings.TrashRetentionDays, Is.EqualTo(30));
    }

    [Test]
    public async Task SetTrashRetentionAsync_ByAnAdmin_IsStored()
    {
        await _service.SetTrashRetentionAsync(_admin.Id, 7);

        var settings = await _service.GetAsync();
        Assert.That(settings.TrashRetentionDays, Is.EqualTo(7));
    }

    [Test]
    public async Task SetTrashRetentionAsync_ByANonAdmin_IsForbiddenAndChangesNothing()
    {
        Assert.ThrowsAsync<AdminRequiredException>(() => _service.SetTrashRetentionAsync(_member.Id, 7));

        var settings = await _service.GetAsync();
        Assert.That(settings.TrashRetentionDays, Is.EqualTo(30));
    }

    [Test]
    public void SetTrashRetentionAsync_ByAnAgent_IsForbidden()
    {
        var agent = TestUsers.AddAgentFlaggedAsAdmin(_db, "Nightly triage");

        Assert.ThrowsAsync<AgentAdministrationException>(() => _service.SetTrashRetentionAsync(agent.Id, 7));
    }

    [TestCase(0)]
    [TestCase(3651)]
    public async Task SetTrashRetentionAsync_OutOfRange_IsAValidationErrorAndChangesNothing(int days)
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _service.SetTrashRetentionAsync(_admin.Id, days));

        var settings = await _service.GetAsync();
        Assert.That(settings.TrashRetentionDays, Is.EqualTo(30));
    }
}
