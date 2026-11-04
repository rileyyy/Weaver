using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>"Changes since T", read through <see cref="IWorkItemChangesService"/> after real writes.</summary>
[TestFixture]
public class WorkItemChangesServiceTests
{
    private static readonly DateTimeOffset Yesterday = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Today = Yesterday.AddDays(1);

    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private WorkItemChangesService _changes = null!;
    private User _alice = null!;
    private User _bob = null!;
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(Yesterday);
        _db = TestDatabase.Create(_clock);
        _alice = TestUsers.Add(_db, "alice");
        _bob = TestUsers.Add(_db, "bob");
        _home = TestSpaces.Add(_db, "Home");
        _caller = new FakeCallerContext(_alice.Id);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), _caller);
        _trash = new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), _caller, _clock);
        _changes = new WorkItemChangesService(_db, _caller);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task AFieldChangeSinceT_ListsTheItemAsASummary_WithOnlyTheEntriesSinceT()
    {
        var item = await CreateAsync("Paint the hall");
        _clock.Advance(TimeSpan.FromDays(1));
        await _workItems.ChangeStatusAsync(item.Id, _home.Doing);
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Paint the hallway" });

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today));

        var changed = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(changed.Deleted, Is.False);
            Assert.That(changed.Item.Id, Is.EqualTo(item.Id));
            Assert.That(changed.Item.Number, Is.EqualTo(item.Number));
            Assert.That(changed.Item.Title, Is.EqualTo("Paint the hallway"));
            Assert.That(changed.Item.SpaceName, Is.EqualTo("Home"));
            Assert.That(changed.Item.StatusName, Is.EqualTo("Doing"));
            Assert.That(changed.Item.ChildCount, Is.EqualTo(0));
            Assert.That(changed.Changes.Select(c => c.Field), Is.EqualTo(new[] { WorkItemField.StatusId, WorkItemField.Title }));
            Assert.That(changed.Changes[0].NewName, Is.EqualTo("Doing"));
            Assert.That(changed.Changes[0].AccountName, Is.EqualTo("alice"));
            Assert.That(page.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task AnItemDeletedSinceT_IsListedAndMarkedDeleted_WithWhatHistoryStillKnows()
    {
        var item = await CreateAsync("Old shed");
        _clock.Advance(TimeSpan.FromDays(1));
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Old garden shed" });
        await _trash.MoveToTrashAsync(item.Id);

        var changed = (await _changes.ListSinceAsync(new WorkItemChangesQuery(Today))).Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(changed.Deleted, Is.True);
            Assert.That(changed.Item.Id, Is.EqualTo(item.Id));
            Assert.That(changed.Item.Title, Is.EqualTo("Old garden shed"));
            Assert.That(changed.Item.SpaceId, Is.EqualTo(_home.Id));
            Assert.That(changed.Item.SpaceName, Is.EqualTo("Home"));
            Assert.That(changed.Item.Number, Is.Null);
            Assert.That(changed.Item.StatusName, Is.Null);
            Assert.That(changed.Changes.Select(c => c.Field), Is.EqualTo(new[] { WorkItemField.Title, WorkItemHistoryField.Deleted }));
        });
    }

    [Test]
    public async Task ACascadingDelete_ListsEveryDeletedItem()
    {
        var parent = await CreateAsync("Kitchen");
        var child = await CreateAsync("Tiles", parent.Id);
        _clock.Advance(TimeSpan.FromDays(1));

        await _trash.MoveToTrashAsync(parent.Id, cascade: true);

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today));
        Assert.That(
            page.Items.Select(i => (i.Item.Id, i.Deleted)),
            Is.EquivalentTo(new[] { (parent.Id, true), (child.Id, true) }));
    }

    [Test]
    public async Task AnItemCreatedAndDeletedSinceT_IsListedOnce_AsDeleted()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        var item = await CreateAsync("Typo");
        await _trash.MoveToTrashAsync(item.Id);

        var changed = (await _changes.ListSinceAsync(new WorkItemChangesQuery(Today))).Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(changed.Deleted, Is.True);
            Assert.That(changed.Item.Title, Is.EqualTo("Typo"));
            Assert.That(changed.Changes.Select(c => c.Field), Is.EqualTo(new[]
            {
                WorkItemHistoryField.Created, WorkItemField.SpaceId, WorkItemHistoryField.Deleted,
            }));
        });
    }

    [Test]
    public async Task ExcludingOwnChanges_LeavesOutTheCallersEntries_AndItemsOnlyTheCallerChanged()
    {
        var mine = await CreateAsync("Mine");
        var shared = await CreateAsync("Shared");
        _clock.Advance(TimeSpan.FromDays(1));
        await _workItems.UpdateDetailsAsync(mine.Id, new WorkItemDetailsPatch { Title = "Mine, renamed" });
        await _workItems.UpdateDetailsAsync(shared.Id, new WorkItemDetailsPatch { Title = "Shared, by alice" });
        SignIn(_bob);
        await _workItems.ChangeStatusAsync(shared.Id, _home.Doing);
        SignIn(_alice);

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, ExcludeOwnChanges: true));

        var changed = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(changed.Item.Id, Is.EqualTo(shared.Id));
            Assert.That(changed.Changes.Select(c => (c.Field, c.AccountName)), Is.EqualTo(new[] { (WorkItemField.StatusId, (string?)"bob") }));
        });
    }

    [Test]
    public async Task ExcludingOwnChanges_KeepsWeaversOwnEntries_WhichHaveNoAccount()
    {
        var item = await CreateAsync("Bins");
        _clock.Advance(TimeSpan.FromDays(1));
        _db.WorkItemHistory.Add(WorkItemHistoryEntry.Record(
            item.Id, HistoryActor.System, WorkItemField.StartDate, null, "\"2026-09-03\""));
        await _db.SaveChangesAsync();

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, ExcludeOwnChanges: true));

        var changed = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(changed.Item.Id, Is.EqualTo(item.Id));
            Assert.That(changed.Changes.Single().AccountId, Is.Null);
        });
    }

    [Test]
    public async Task NotExcludingOwnChanges_ListsEveryonesEntries()
    {
        var item = await CreateAsync("Fence");
        _clock.Advance(TimeSpan.FromDays(1));
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Fence, by alice" });
        SignIn(_bob);
        await _workItems.ChangeStatusAsync(item.Id, _home.Doing);

        var changed = (await _changes.ListSinceAsync(new WorkItemChangesQuery(Today))).Items.Single();

        Assert.That(changed.Changes.Select(c => c.AccountName), Is.EqualTo(new[] { "alice", "bob" }));
    }

    [Test]
    public async Task ASpace_LimitsTheListToItsItems_IncludingItsDeletedOnes()
    {
        var village = TestSpaces.Add(_db, "Village");
        var homeItem = await CreateAsync("Boiler");
        var homeDeleted = await CreateAsync("Old boiler");
        var villageItem = await CreateAsync("Fete", space: village);
        var villageDeleted = await CreateAsync("Old fete", space: village);
        _clock.Advance(TimeSpan.FromDays(1));
        await _workItems.ChangeStatusAsync(homeItem.Id, _home.Doing);
        await _workItems.ChangeStatusAsync(villageItem.Id, village.Doing);

        await _trash.MoveToTrashAsync(homeDeleted.Id);
        await _trash.MoveToTrashAsync(villageDeleted.Id);

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, SpaceId: _home.Id));

        Assert.That(
            page.Items.Select(i => (i.Item.Title, i.Deleted)),
            Is.EqualTo(new[] { ("Boiler", false), ("Old boiler", true) }));
    }

    [Test]
    public async Task Paging_WalksEveryItemOnce_InTheOrderEachFirstChanged()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        var c = await CreateAsync("C");
        await _workItems.UpdateDetailsAsync(a.Id, new WorkItemDetailsPatch { Title = "A again" });

        var first = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Limit: 2));
        var second = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Cursor: first.NextCursor, Limit: 2));

        Assert.Multiple(() =>
        {
            Assert.That(first.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { a.Id, b.Id }));
            Assert.That(first.NextCursor, Is.Not.Null);
            Assert.That(first.Items[0].Changes.Select(ch => ch.Field).Last(), Is.EqualTo(WorkItemField.Title));
            Assert.That(second.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { c.Id }));
            Assert.That(second.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task Paging_IsStable_WhenChangesLandBetweenPages()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        var a = await CreateAsync("A");
        var b = await CreateAsync("B");
        var c = await CreateAsync("C");

        var first = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Limit: 2));
        await _workItems.UpdateDetailsAsync(a.Id, new WorkItemDetailsPatch { Title = "A, changed again" });
        await _workItems.UpdateDetailsAsync(c.Id, new WorkItemDetailsPatch { Title = "C, changed" });
        var d = await CreateAsync("D");
        var rest = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Cursor: first.NextCursor, Limit: 2));

        Assert.Multiple(() =>
        {
            Assert.That(first.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { a.Id, b.Id }));
            Assert.That(rest.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { c.Id, d.Id }));
            Assert.That(rest.Items[0].Item.Title, Is.EqualTo("C, changed"));
        });
    }

    [Test]
    public async Task APageThatExactlyFillsTheLimit_HasNoNextCursor_WhenNothingFollows()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        await CreateAsync("A");
        await CreateAsync("B");

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Limit: 2));

        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Has.Count.EqualTo(2));
            Assert.That(page.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task Since_IsInclusive()
    {
        _clock.Advance(TimeSpan.FromDays(1));
        var item = await CreateAsync("Exactly at T");

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today));

        Assert.That(page.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { item.Id }));
    }

    [Test]
    public async Task Since_IsAnInstant_WhateverOffsetItIsWrittenIn()
    {
        var before = await CreateAsync("Before");
        _clock.Advance(TimeSpan.FromDays(1));
        var after = await CreateAsync("After");

        var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(Today.ToOffset(TimeSpan.FromHours(-5))));

        Assert.That(page.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { after.Id }).And.Not.Contains(before.Id));
    }

    [TestCase(0)]
    [TestCase(WorkItemChangesQuery.MaxLimit + 1)]
    public void ALimitOutOfRange_IsRejected(int limit)
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Limit: limit)));
    }

    [Test]
    public void AMalformedCursor_IsRejected()
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _changes.ListSinceAsync(new WorkItemChangesQuery(Today, Cursor: "garbage!")));
    }

    private void SignIn(User user) => _caller.Current = new Caller(user.Id, null, null);

    /// <summary>Children must be of <paramref name="space"/> too, as its To Do is the one they get.</summary>
    private Task<WorkItem> CreateAsync(string title, Guid? parentId = null, TestSpace? space = null)
    {
        space ??= _home;
        return _workItems.CreateAsync(title, null, parentId, space.ToDo, spaceId: parentId is null ? space.Id : null);
    }
}
