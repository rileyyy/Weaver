using System.Text.Json;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class SpaceServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private SpaceService _spaces = null!;
    private User _admin = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _caller = new FakeCallerContext(_admin.Id);
        _spaces = new SpaceService(_db, new AdminAuthorizer(_db), _caller);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ListsSpacesByName()
    {
        await _spaces.CreateAsync("Village");
        await _spaces.CreateAsync("Home");

        var spaces = await _spaces.GetAllAsync();

        Assert.That(spaces.Select(s => s.Name), Is.EqualTo(new[] { "Home", "Village" }));
    }

    [Test]
    public async Task CreateAsync_StartsTheSpaceWithTheDefaultStatusesAndPriorities()
    {
        var space = await _spaces.CreateAsync("Home");

        var statuses = _db.Statuses.Where(s => s.SpaceId == space.Id).OrderBy(s => s.Order).ToList();
        var priorities = _db.WorkItemPriorities.Where(p => p.SpaceId == space.Id).OrderBy(p => p.Order).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(statuses.Select(s => (s.Name, s.Category)), Is.EqualTo(new[]
            {
                ("To Do", StatusCategory.ToDo),
                ("Doing", StatusCategory.Doing),
                ("Done", StatusCategory.Done),
            }));
            Assert.That(priorities.Select(p => p.Name), Is.EqualTo(new[] { "Low", "Medium", "High", "Urgent" }));
        });
    }

    [Test]
    public async Task CreateAsync_GivesEachSpaceItsOwnDefaults()
    {
        var home = await _spaces.CreateAsync("Home");
        var village = await _spaces.CreateAsync("Village");

        var homeIds = _db.Statuses.Where(s => s.SpaceId == home.Id).Select(s => s.Id).ToList();
        var villageIds = _db.Statuses.Where(s => s.SpaceId == village.Id).Select(s => s.Id).ToList();
        Assert.That(homeIds, Has.Count.EqualTo(3).And.No.AnyOf(villageIds.Cast<object>().ToArray()));
    }

    [Test]
    public async Task CreateAsync_RecordsWhoCreatedItAndItsName()
    {
        var space = await _spaces.CreateAsync("Home");

        var entry = _db.SpaceHistory.Single(e => e.SpaceId == space.Id);
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(SpaceHistoryField.Created));
            Assert.That(entry.AccountId, Is.EqualTo(_admin.Id));
            Assert.That(entry.OldValue, Is.Null);
            Assert.That(entry.NewValue, Is.EqualTo(JsonSerializer.Serialize("Home")));
        });
    }

    [Test]
    public async Task CreateAsync_WithANameAlreadyTaken_IsAConflict()
    {
        await _spaces.CreateAsync("Home");

        Assert.ThrowsAsync<SpaceNameTakenException>(() => _spaces.CreateAsync(" home "));
    }

    [Test]
    public void CreateAsync_ByANonAdmin_IsForbiddenAndRecordsNothing()
    {
        _caller.Current = new Caller(TestUsers.Add(_db, "member").Id, null, null);

        Assert.ThrowsAsync<AdminRequiredException>(() => _spaces.CreateAsync("Home"));
        Assert.That(_db.Spaces.Any(), Is.False);
    }

    [Test]
    public void CreateAsync_ByAnAgent_IsForbidden()
    {
        var agent = User.CreateAgent("Claude", null);
        _db.Users.Add(agent);
        _db.SaveChanges();
        _caller.Current = new Caller(agent.Id, Guid.NewGuid(), null);

        Assert.ThrowsAsync<AgentAdministrationException>(() => _spaces.CreateAsync("Home"));
    }

    [Test]
    public async Task RenameAsync_ChangesTheNameAndRecordsTheOldAndNew()
    {
        var space = await _spaces.CreateAsync("Home");

        var renamed = await _spaces.RenameAsync(space.Id, "House");

        var entry = _db.SpaceHistory.OrderBy(e => e.Id).Last(e => e.SpaceId == space.Id);
        Assert.Multiple(() =>
        {
            Assert.That(renamed.Name, Is.EqualTo("House"));
            Assert.That(entry.Field, Is.EqualTo(SpaceHistoryField.Name));
            Assert.That(entry.OldValue, Is.EqualTo(JsonSerializer.Serialize("Home")));
            Assert.That(entry.NewValue, Is.EqualTo(JsonSerializer.Serialize("House")));
        });
    }

    [Test]
    public async Task RenameAsync_ToTheSameName_RecordsNothing()
    {
        var space = await _spaces.CreateAsync("Home");

        await _spaces.RenameAsync(space.Id, "Home");

        Assert.That(_db.SpaceHistory.Count(e => e.SpaceId == space.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task RenameAsync_ToAnotherSpacesName_IsAConflict()
    {
        await _spaces.CreateAsync("Home");
        var village = await _spaces.CreateAsync("Village");

        Assert.ThrowsAsync<SpaceNameTakenException>(() => _spaces.RenameAsync(village.Id, "HOME"));
    }

    [Test]
    public void RenameAsync_AnUnknownSpace_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _spaces.RenameAsync(Guid.NewGuid(), "Home"));
    }

    [Test]
    public async Task RenameAsync_ByANonAdmin_IsForbidden()
    {
        var space = await _spaces.CreateAsync("Home");
        _caller.Current = new Caller(TestUsers.Add(_db, "member").Id, null, null);

        Assert.ThrowsAsync<AdminRequiredException>(() => _spaces.RenameAsync(space.Id, "House"));
    }
}
