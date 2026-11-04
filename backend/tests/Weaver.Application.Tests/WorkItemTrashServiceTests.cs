using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>Deleting to the Trash, listing it and restoring from it (ADR-0007).</summary>
[TestFixture]
public class WorkItemTrashServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private User _alice = null!;
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(Now);
        _db = TestDatabase.Create(_clock);
        _alice = TestUsers.Add(_db, "alice");
        _home = TestSpaces.Add(_db, "Home");
        _caller = new FakeCallerContext(_alice.Id);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        _workItems = new WorkItemService(_db, hierarchy, _caller);
        _trash = new WorkItemTrashService(_db, hierarchy, _caller, _clock);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private Task<WorkItem> CreateAsync(string title, Guid? parentId = null, TestSpace? space = null) =>
        _workItems.CreateAsync(title, null, parentId, (space ?? _home).ToDo, spaceId: parentId is null ? (space ?? _home).Id : null);

    private Task<WorkItem> RowAsync(Guid id) => _db.WorkItems.IgnoreQueryFilters().AsNoTracking().SingleAsync(w => w.Id == id);

    [Test]
    public async Task MoveToTrash_HidesTheItemAndItsSubtreeFromReads_ButKeepsTheRows()
    {
        var root = await CreateAsync("Kitchen");
        var child = await CreateAsync("Tiles", root.Id);
        var grandchild = await CreateAsync("Grout", child.Id);
        var other = await CreateAsync("Garden");

        await _trash.MoveToTrashAsync(root.Id, cascade: true);

        Assert.Multiple(async () =>
        {
            Assert.That(await _workItems.GetByIdAsync(root.Id), Is.Null);
            Assert.That(await _workItems.GetByIdAsync(grandchild.Id), Is.Null);
            Assert.That(await _workItems.GetChildrenAsync(root.Id), Is.Empty);
            Assert.That((await _workItems.GetAllAsync(0, 50)).Select(w => w.Id), Is.EqualTo(new[] { other.Id }));
            Assert.That((await RowAsync(root.Id)).TrashRootId, Is.EqualTo(root.Id));
            Assert.That((await RowAsync(child.Id)).TrashRootId, Is.EqualTo(root.Id));
            Assert.That((await RowAsync(grandchild.Id)).TrashedAtUtc, Is.EqualTo(Now));
        });
    }

    [Test]
    public async Task MoveToTrash_WithChildren_WithoutCascade_IsRejected()
    {
        var parent = await CreateAsync("Parent");
        await CreateAsync("Child", parent.Id);

        Assert.ThrowsAsync<WorkItemHasChildrenException>(() => _trash.MoveToTrashAsync(parent.Id));
    }

    [Test]
    public async Task MoveToTrash_AParentWhoseOnlyChildIsAlreadyTrashed_NeedsNoCascade()
    {
        var parent = await CreateAsync("Parent");
        var child = await CreateAsync("Child", parent.Id);
        await _trash.MoveToTrashAsync(child.Id);

        await _trash.MoveToTrashAsync(parent.Id);

        Assert.That((await RowAsync(child.Id)).TrashRootId, Is.EqualTo(child.Id), "the child stays in its own delete");
    }

    [Test]
    public async Task MoveToTrash_WithStaleExpectedVersion_TrashesNothing()
    {
        var item = await CreateAsync("Item");

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _trash.MoveToTrashAsync(item.Id, expectedVersion: item.Version + 1));
        Assert.That(await _workItems.GetByIdAsync(item.Id), Is.Not.Null);
    }

    [Test]
    public async Task MoveToTrash_AnItemAlreadyInTheTrash_IsNotFound()
    {
        var item = await CreateAsync("Item");
        await _trash.MoveToTrashAsync(item.Id);

        Assert.ThrowsAsync<EntityNotFoundException>(() => _trash.MoveToTrashAsync(item.Id));
    }

    [Test]
    public async Task MoveToTrash_KeepsLinksAndComments_ButHidesThemFromTheSurvivingSide()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var comments = new CommentService(_db, _clock, _caller);
        var item = await CreateAsync("Item");
        var survivor = await CreateAsync("Survivor");
        await links.CreateAsync(survivor.Id, item.Id);
        await comments.CreateAsync(item.Id, "Note");

        await _trash.MoveToTrashAsync(item.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await links.ListForWorkItemAsync(survivor.Id), Is.Empty);
            Assert.That(await _db.WorkItemLinks.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
            Assert.That(await _db.Comments.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task MoveToTrash_RecordsADeleteForTheItemAndEachDescendant()
    {
        var history = new WorkItemHistoryService(_db);
        var root = await CreateAsync("Root");
        var child = await CreateAsync("Child", root.Id);

        await _trash.MoveToTrashAsync(root.Id, cascade: true);

        Assert.Multiple(async () =>
        {
            var rootEntry = (await history.ListForWorkItemAsync(root.Id)).First();
            Assert.That(rootEntry.Field, Is.EqualTo(WorkItemHistoryField.Deleted));
            Assert.That(rootEntry.OldValue?.GetString(), Is.EqualTo("Root"));
            Assert.That(rootEntry.AccountName, Is.EqualTo("alice"));
            Assert.That((await history.ListForWorkItemAsync(child.Id)).First().Field, Is.EqualTo(WorkItemHistoryField.Deleted));
        });
    }

    [Test]
    public async Task List_ShowsEachDeleteOnce_NewestFirst_WithWhoAndWhenAndHowManyItems()
    {
        var bob = TestUsers.Add(_db, "bob");
        var kitchen = await CreateAsync("Kitchen");
        await CreateAsync("Tiles", kitchen.Id);
        var shed = await CreateAsync("Shed");
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);
        _clock.Advance(TimeSpan.FromHours(1));
        _caller.Current = new Caller(bob.Id, null, "tidy-up");
        await _trash.MoveToTrashAsync(shed.Id);

        var trash = await _trash.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(trash.Select(t => t.Item.Title), Is.EqualTo(new[] { "Shed", "Kitchen" }));
            Assert.That(trash[0].TrashedAtUtc, Is.EqualTo(Now.AddHours(1)));
            Assert.That(trash[0].DeletedByName, Is.EqualTo("bob"));
            Assert.That(trash[0].DeletedByActorLabel, Is.EqualTo("tidy-up"));
            Assert.That(trash[0].ItemCount, Is.EqualTo(1));
            Assert.That(trash[1].DeletedByName, Is.EqualTo("alice"));
            Assert.That(trash[1].ItemCount, Is.EqualTo(2));
            Assert.That(trash[1].Item.SpaceName, Is.EqualTo("Home"));
            Assert.That(trash[1].Item.TrashedAtUtc, Is.EqualTo(Now));
        });
    }

    [Test]
    public async Task List_ForASpace_ShowsOnlyThatSpacesTrash()
    {
        var village = TestSpaces.Add(_db, "Village");
        var boiler = await CreateAsync("Boiler");
        var fete = await CreateAsync("Fete", space: village);
        await _trash.MoveToTrashAsync(boiler.Id);
        await _trash.MoveToTrashAsync(fete.Id);

        var trash = await _trash.ListAsync(village.Id);

        Assert.That(trash.Select(t => t.Item.Id), Is.EqualTo(new[] { fete.Id }));
    }

    [Test]
    public async Task Restore_BringsBackTheItemAndItsSubtree_UnderTheSameParent()
    {
        var house = await CreateAsync("House");
        var kitchen = await CreateAsync("Kitchen", house.Id);
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);

        var restored = await _trash.RestoreAsync(kitchen.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(restored.ParentId, Is.EqualTo(house.Id));
            Assert.That(restored.IsTrashed, Is.False);
            Assert.That((await _workItems.GetByIdAsync(tiles.Id))?.ParentId, Is.EqualTo(kitchen.Id));
            Assert.That(await _trash.ListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task Restore_DoesNotBringBackAChildDeletedSeparatelyBefore()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(tiles.Id);
        await _trash.MoveToTrashAsync(kitchen.Id);

        await _trash.RestoreAsync(kitchen.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await _workItems.GetByIdAsync(tiles.Id), Is.Null);
            Assert.That((await _trash.ListAsync()).Select(t => t.Item.Id), Is.EqualTo(new[] { tiles.Id }));
        });
    }

    [Test]
    public async Task Restore_WhenItsParentIsStillTrashed_GoesUnderTheNearestAncestorThatExists()
    {
        var house = await CreateAsync("House");
        var kitchen = await CreateAsync("Kitchen", house.Id);
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(tiles.Id);
        await _trash.MoveToTrashAsync(kitchen.Id);

        var restored = await _trash.RestoreAsync(tiles.Id);

        Assert.That(restored.ParentId, Is.EqualTo(house.Id));
    }

    [Test]
    public async Task Restore_WhenNoAncestorExists_BecomesARootOfItsSpace()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(tiles.Id);
        await _trash.MoveToTrashAsync(kitchen.Id);

        var restored = await _trash.RestoreAsync(tiles.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(restored.ParentId, Is.Null);
            Assert.That(restored.SpaceId, Is.EqualTo(_home.Id));
            Assert.That((await _workItems.GetChildrenAsync(null, _home.Id)).Select(w => w.Id), Does.Contain(tiles.Id));
        });
    }

    [Test]
    public async Task Restore_WhenItsParentWasPurged_BecomesARootOfItsSpace()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(tiles.Id);
        await PurgeAsync(kitchen.Id);

        var restored = await _trash.RestoreAsync(tiles.Id);

        Assert.That(restored.ParentId, Is.Null);
    }

    [Test]
    public async Task Restore_ToANewParent_RecordsTheMoveAsWellAsTheRestore()
    {
        var history = new WorkItemHistoryService(_db);
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        var grout = await CreateAsync("Grout", tiles.Id);
        await _trash.MoveToTrashAsync(tiles.Id, cascade: true);
        await _trash.MoveToTrashAsync(kitchen.Id);

        await _trash.RestoreAsync(tiles.Id);

        Assert.Multiple(async () =>
        {
            var latest = (await history.ListForWorkItemAsync(tiles.Id)).Take(2).Select(e => e.Field);
            Assert.That(latest, Is.EquivalentTo(new[] { WorkItemHistoryField.Restored, WorkItemField.ParentId }));
            Assert.That((await history.ListForWorkItemAsync(grout.Id)).First().Field, Is.EqualTo(WorkItemHistoryField.Restored));
        });
    }

    [Test]
    public async Task Restore_AnItemTrashedWithAnAncestor_IsRejected_NamingWhatToRestore()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);

        var ex = Assert.ThrowsAsync<DomainValidationException>(() => _trash.RestoreAsync(tiles.Id));
        Assert.That(ex!.Message, Does.Contain(kitchen.Id.ToString()));
    }

    [Test]
    public async Task Restore_ALiveItem_IsRejected()
    {
        var item = await CreateAsync("Item");

        Assert.ThrowsAsync<DomainValidationException>(() => _trash.RestoreAsync(item.Id));
    }

    [Test]
    public void Restore_AnUnknownItem_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _trash.RestoreAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task Restore_ShowsTheItemsLinksAndCommentsAgain()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var comments = new CommentService(_db, _clock, _caller);
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");
        await links.CreateAsync(other.Id, item.Id);
        await comments.CreateAsync(item.Id, "Note");
        await _trash.MoveToTrashAsync(item.Id);

        await _trash.RestoreAsync(item.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await links.ListForWorkItemAsync(other.Id), Has.Count.EqualTo(1));
            Assert.That(await comments.ListForWorkItemAsync(item.Id), Has.Count.EqualTo(1));
        });
    }

    /// <summary>Stands in for ticket 20's purge: the row goes, its history stays.</summary>
    private async Task PurgeAsync(Guid id)
    {
        // A purge runs in its own request, so nothing it orphans is tracked.
        _db.ChangeTracker.Clear();
        var row = await _db.WorkItems.IgnoreQueryFilters().SingleAsync(w => w.Id == id);
        _db.WorkItems.Remove(row);
        await _db.SaveChangesAsync();
    }
}
