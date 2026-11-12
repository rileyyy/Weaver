using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>What <see cref="SpaceService"/> records, read back through <see cref="ISpaceHistoryService"/>.</summary>
[TestFixture]
public class SpaceHistoryServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private SpaceService _spaces = null!;
    private SpaceHistoryService _history = null!;
    private User _admin = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _caller = new FakeCallerContext(_admin.Id);
        _spaces = new SpaceService(_db, new AdminAuthorizer(_db), _caller);
        _history = new SpaceHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task ListForSpaceAsync_ListsEveryChangeNewestFirst()
    {
        var space = await _spaces.CreateAsync("Home");
        await _spaces.RenameAsync(space.Id, "House");
        await _spaces.SetDoneHidingPeriodAsync(space.Id, 30);

        var entries = await _history.ListForSpaceAsync(space.Id);

        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => e.Field), Is.EqualTo(new[]
            {
                SpaceHistoryField.DoneHidingDays,
                SpaceHistoryField.Name,
                SpaceHistoryField.Created,
            }));
            Assert.That(entries[0].OldValue?.GetInt32(), Is.EqualTo(Space.DefaultDoneHidingDays));
            Assert.That(entries[0].NewValue?.GetInt32(), Is.EqualTo(30));
            Assert.That(entries[1].OldValue?.GetString(), Is.EqualTo("Home"));
            Assert.That(entries[1].NewValue?.GetString(), Is.EqualTo("House"));
            Assert.That(entries[2].OldValue, Is.Null);
            Assert.That(entries[2].NewValue?.GetString(), Is.EqualTo("Home"));
        });
    }

    [Test]
    public async Task ListForSpaceAsync_SaysWhoMadeEachChangeAndWhen()
    {
        var space = await _spaces.CreateAsync("Home");

        var entry = (await _history.ListForSpaceAsync(space.Id)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.SpaceId, Is.EqualTo(space.Id));
            Assert.That(entry.ChangedAtUtc, Is.EqualTo(_clock.GetUtcNow()));
            Assert.That(entry.AccountId, Is.EqualTo(_admin.Id));
            Assert.That(entry.AccountName, Is.EqualTo("admin"));
            Assert.That(entry.AccountKind, Is.EqualTo(UserKind.Human));
            Assert.That(entry.ApiKeyId, Is.Null);
            Assert.That(entry.ApiKeyLabel, Is.Null);
            Assert.That(entry.ActorLabel, Is.Null);
        });
    }

    [Test]
    public async Task ListForSpaceAsync_NamesTheKeyAndActorLabelAChangeWasMadeWith()
    {
        // Agents can't change a Space today, so the entry is written directly: the reader shouldn't
        // depend on that rule to name who made a change.
        var space = await _spaces.CreateAsync("Home");
        var agent = User.CreateAgent("Claude", null);
        var key = ApiKey.Issue(agent, "nightly", ApiKeyScope.ReadWrite, "HASH");
        _db.Users.Add(agent);
        _db.ApiKeys.Add(key);
        _db.SpaceHistory.Add(SpaceHistoryEntry.Record(
            space.Id, HistoryActor.Create(agent.Id, key.Id, "triage", null), SpaceHistoryField.Name, "\"Home\"", "\"House\""));
        _db.SaveChanges();

        var entry = (await _history.ListForSpaceAsync(space.Id)).First();

        Assert.Multiple(() =>
        {
            Assert.That(entry.AccountName, Is.EqualTo("Claude"));
            Assert.That(entry.AccountKind, Is.EqualTo(UserKind.Agent));
            Assert.That(entry.ApiKeyId, Is.EqualTo(key.Id));
            Assert.That(entry.ApiKeyLabel, Is.EqualTo("nightly"));
            Assert.That(entry.ActorLabel, Is.EqualTo("triage"));
        });
    }

    [Test]
    public async Task ListForSpaceAsync_LeavesOutOtherSpaces()
    {
        var home = await _spaces.CreateAsync("Home");
        await _spaces.CreateAsync("Village");

        var entries = await _history.ListForSpaceAsync(home.Id);

        Assert.That(entries.Select(e => e.SpaceId), Is.EqualTo(new[] { home.Id }));
    }

    [Test]
    public void ListForSpaceAsync_AnUnknownSpace_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _history.ListForSpaceAsync(Guid.NewGuid()));
    }
}
