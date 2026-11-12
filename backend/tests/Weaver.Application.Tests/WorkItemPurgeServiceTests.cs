using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>Purging the Trash by hand and on expiry (ADR-0007).</summary>
[TestFixture]
public class WorkItemPurgeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private WorkItemPurgeService _purge = null!;
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
        _workItems = new WorkItemService(_db, hierarchy, new FixedServerCalendar(), _caller);
        _trash = new WorkItemTrashService(_db, hierarchy, _caller, _clock);
        _purge = new WorkItemPurgeService(_db, new AttachmentFileCleanup(_db, new InMemoryAttachmentStore()), _caller, _clock);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private Task<WorkItem> CreateAsync(string title, Guid? parentId = null) =>
        _workItems.CreateAsync(title, null, parentId, _home.ToDo, spaceId: parentId is null ? _home.Id : null);

    private Task<bool> RowExistsAsync(Guid id) => _db.WorkItems.IgnoreQueryFilters().AnyAsync(w => w.Id == id);

    [Test]
    public async Task Purge_RemovesTheDeletedItemAndEverythingDeletedWithIt()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        var grout = await CreateAsync("Grout", tiles.Id);
        var shed = await CreateAsync("Shed");
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);

        await _purge.PurgeAsync(kitchen.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await RowExistsAsync(kitchen.Id), Is.False);
            Assert.That(await RowExistsAsync(tiles.Id), Is.False);
            Assert.That(await RowExistsAsync(grout.Id), Is.False);
            Assert.That(await RowExistsAsync(shed.Id), Is.True);
            Assert.That(await _trash.ListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task Purge_AnItemDeletedWithAnAncestor_IsRejected_NamingWhatToPurge()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);

        var ex = Assert.ThrowsAsync<DomainValidationException>(() => _purge.PurgeAsync(tiles.Id));

        Assert.Multiple(async () =>
        {
            Assert.That(ex!.Message, Does.Contain(kitchen.Id.ToString()));
            Assert.That(await RowExistsAsync(tiles.Id), Is.True);
        });
    }

    [Test]
    public async Task Purge_ALiveItem_IsRejected()
    {
        var item = await CreateAsync("Item");

        Assert.ThrowsAsync<DomainValidationException>(() => _purge.PurgeAsync(item.Id));
        Assert.That(await RowExistsAsync(item.Id), Is.True);
    }

    [Test]
    public void Purge_AnUnknownItem_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _purge.PurgeAsync(Guid.NewGuid()));
    }

    // Read-write agents can delete and restore, but purging is for humans only (ADR-0007).
    [Test]
    public async Task Purge_ByAReadWriteAgent_IsForbidden_AndPurgesNothing()
    {
        var item = await CreateAsync("Item");
        await _trash.MoveToTrashAsync(item.Id);
        UseAgentCaller();

        var ex = Assert.ThrowsAsync<AgentPurgeException>(() => _purge.PurgeAsync(item.Id));

        Assert.Multiple(async () =>
        {
            Assert.That(ex!.Kind, Is.EqualTo(DomainErrorKind.Forbidden));
            Assert.That(await RowExistsAsync(item.Id), Is.True);
        });
    }

    [Test]
    public async Task Purge_ByAnAdmin_IsAllowed()
    {
        var item = await CreateAsync("Item");
        await _trash.MoveToTrashAsync(item.Id);
        _caller.Current = new Caller(TestUsers.Add(_db, "root", isAdmin: true).Id, null, null);

        await _purge.PurgeAsync(item.Id);

        Assert.That(await RowExistsAsync(item.Id), Is.False);
    }

    [Test]
    public async Task Purge_KeepsAChildDeletedSeparately_InTheTrash_AsARootOfItsSpace()
    {
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(tiles.Id);
        await _trash.MoveToTrashAsync(kitchen.Id);
        InAFreshRequest();

        await _purge.PurgeAsync(kitchen.Id);

        var survivor = await _db.WorkItems.IgnoreQueryFilters().AsNoTracking().SingleAsync(w => w.Id == tiles.Id);
        Assert.Multiple(async () =>
        {
            Assert.That(survivor.IsTrashRoot, Is.True);
            Assert.That(survivor.ParentId, Is.Null);
            Assert.That((await _trash.RestoreAsync(tiles.Id)).ParentId, Is.Null);
        });
    }

    [Test]
    public async Task Purge_RemovesLinksToSurvivors_AndRecordsTheRemovalOnTheSurvivor()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var shed = await CreateAsync("Shed");
        var garden = await CreateAsync("Garden");
        await links.CreateAsync(shed.Id, garden.Id, WorkItemLinkRelation.Blocks);
        await _trash.MoveToTrashAsync(shed.Id);

        await _purge.PurgeAsync(shed.Id);

        var removal = (await new WorkItemHistoryService(_db).ListForWorkItemAsync(garden.Id)).First();
        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItemLinks.IgnoreQueryFilters().AnyAsync(), Is.False);
            Assert.That(removal.Field, Is.EqualTo(WorkItemHistoryField.Link));
            Assert.That(removal.OldValue?.GetRawText(), Is.EqualTo($"{{\"workItemId\":\"{shed.Id}\",\"type\":\"BlockedBy\"}}"));
            Assert.That(removal.NewValue, Is.Null);
            Assert.That(removal.AccountName, Is.EqualTo("alice"));
        });
    }

    [Test]
    public async Task Purge_RemovesLinksWithinTheDelete_WithoutRecordingThemAsRemovals()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await links.CreateAsync(kitchen.Id, tiles.Id, WorkItemLinkRelation.Related);
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);

        await _purge.PurgeAsync(kitchen.Id);

        var linkRemovals = await _db.WorkItemHistory.CountAsync(e => e.Field == WorkItemHistoryField.Link && e.NewValue == null);
        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItemLinks.IgnoreQueryFilters().AnyAsync(), Is.False);
            Assert.That(linkRemovals, Is.Zero);
        });
    }

    [Test]
    public async Task Purge_RemovesTheItemsComments_UrlLinks_Attachments_AndRepeatSchedule()
    {
        var store = new InMemoryAttachmentStore();
        _purge = new WorkItemPurgeService(_db, new AttachmentFileCleanup(_db, store), _caller, _clock);
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        var shed = await CreateAsync("Shed");
        await new CommentService(_db, _clock, _caller).CreateAsync(tiles.Id, "Grey grout");
        await new CommentService(_db, _clock, _caller).CreateAsync(shed.Id, "Kept");
        await new WorkItemUrlLinkService(_db, _caller).CreateAsync(kitchen.Id, "https://example.com/tiles", null);
        var attachments = new WorkItemAttachmentService(
            _db, _caller, store, new FakeThumbnailGenerator(), Options.Create(new AttachmentOptions()), NullLogger<WorkItemAttachmentService>.Instance);
        await attachments.AddAsync(tiles.Id, "plan.txt", "text/plain", new MemoryStream([1]));
        _db.WorkItemRecurrences.Add(WorkItemRecurrence.Create(
            kitchen.Id, RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, [DayOfWeek.Monday], new DateOnly(2026, 11, 9), null)));
        await _db.SaveChangesAsync();
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);
        InAFreshRequest();

        await _purge.PurgeAsync(kitchen.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.Comments.IgnoreQueryFilters().Select(c => c.Body).ToListAsync(), Is.EqualTo(new[] { "Kept" }));
            Assert.That(await _db.WorkItemUrlLinks.IgnoreQueryFilters().AnyAsync(), Is.False);
            Assert.That(await _db.WorkItemAttachments.IgnoreQueryFilters().AnyAsync(), Is.False);
            Assert.That(store.Files, Is.Empty);
            Assert.That(await _db.WorkItemRecurrences.IgnoreQueryFilters().AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task Purge_RecordsAPurgeOnEveryItem_AndKeepsTheirHistory()
    {
        var history = new WorkItemHistoryService(_db);
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);

        await _purge.PurgeAsync(kitchen.Id);

        Assert.Multiple(async () =>
        {
            var kitchenHistory = await history.ListForWorkItemAsync(kitchen.Id);
            Assert.That(kitchenHistory.Select(e => e.Field), Does.Contain(WorkItemHistoryField.Created));
            Assert.That(kitchenHistory[0].Field, Is.EqualTo(WorkItemHistoryField.Purged));
            Assert.That(kitchenHistory[0].OldValue?.GetString(), Is.EqualTo("Kitchen"));
            Assert.That(kitchenHistory[0].AccountName, Is.EqualTo("alice"));
            var tilesLatest = (await history.ListForWorkItemAsync(tiles.Id))[0];
            Assert.That(tilesLatest.Field, Is.EqualTo(WorkItemHistoryField.Purged));
            Assert.That(tilesLatest.OldValue?.GetString(), Is.EqualTo("Tiles"));
        });
    }

    [Test]
    public async Task EmptyTrash_PurgesEveryDelete_InEverySpace_AndLeavesLiveItems()
    {
        var village = TestSpaces.Add(_db, "Village");
        var kitchen = await CreateAsync("Kitchen");
        var tiles = await CreateAsync("Tiles", kitchen.Id);
        var fete = await _workItems.CreateAsync("Fete", null, null, village.ToDo, spaceId: village.Id);
        var garden = await CreateAsync("Garden");
        await _trash.MoveToTrashAsync(tiles.Id);
        await _trash.MoveToTrashAsync(kitchen.Id);
        await _trash.MoveToTrashAsync(fete.Id);

        await _purge.EmptyTrashAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItems.IgnoreQueryFilters().Select(w => w.Id).ToListAsync(), Is.EqualTo(new[] { garden.Id }));
            Assert.That(await _trash.ListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task EmptyTrash_ByAReadWriteAgent_IsForbidden_AndPurgesNothing()
    {
        var item = await CreateAsync("Item");
        await _trash.MoveToTrashAsync(item.Id);
        UseAgentCaller();

        Assert.ThrowsAsync<AgentPurgeException>(() => _purge.EmptyTrashAsync());
        Assert.That(await RowExistsAsync(item.Id), Is.True);
    }

    [Test]
    public void EmptyTrash_WhenTheTrashIsEmpty_DoesNothing()
    {
        Assert.DoesNotThrowAsync(() => _purge.EmptyTrashAsync());
    }

    [Test]
    public async Task PurgeExpired_PurgesDeletesOlderThanTheRetentionPeriod_AndKeepsNewerOnes()
    {
        await SetRetentionDaysAsync(7);
        var kitchen = await CreateAsync("Kitchen");
        await CreateAsync("Tiles", kitchen.Id);
        var shed = await CreateAsync("Shed");
        var garden = await CreateAsync("Garden");
        await _trash.MoveToTrashAsync(kitchen.Id, cascade: true);
        _clock.Advance(TimeSpan.FromDays(3));
        await _trash.MoveToTrashAsync(shed.Id);
        _clock.Advance(TimeSpan.FromDays(5));

        var purged = await _purge.PurgeExpiredAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(purged, Is.EqualTo(2));
            Assert.That(await RowExistsAsync(kitchen.Id), Is.False);
            Assert.That(await RowExistsAsync(shed.Id), Is.True);
            Assert.That(await RowExistsAsync(garden.Id), Is.True);
        });
    }

    // Elapsed time since the delete: it expires exactly retention × 24 hours later, not at a
    // calendar-day boundary.
    [Test]
    public async Task PurgeExpired_ADeleteExpiresExactlyWhenItsRetentionPeriodHasPassed()
    {
        await SetRetentionDaysAsync(2);
        var item = await CreateAsync("Item");
        await _trash.MoveToTrashAsync(item.Id);

        _clock.Advance(TimeSpan.FromDays(2) - TimeSpan.FromSeconds(1));
        var early = await _purge.PurgeExpiredAsync();
        _clock.Advance(TimeSpan.FromSeconds(1));
        var onTime = await _purge.PurgeExpiredAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(early, Is.Zero);
            Assert.That(onTime, Is.EqualTo(1));
            Assert.That(await RowExistsAsync(item.Id), Is.False);
        });
    }

    // The background job has no caller; reading one would throw outside a request.
    [Test]
    public async Task PurgeExpired_RecordsThePurgeAsWeaverItself_WithoutReadingACaller()
    {
        var item = await CreateAsync("Item");
        await _trash.MoveToTrashAsync(item.Id);
        _clock.Advance(TimeSpan.FromDays(ServerSettings.DefaultTrashRetentionDays));
        var purge = new WorkItemPurgeService(_db, new AttachmentFileCleanup(_db, new InMemoryAttachmentStore()), new NoCallerContext(), _clock);

        await purge.PurgeExpiredAsync();

        var entry = (await new WorkItemHistoryService(_db).ListForWorkItemAsync(item.Id))[0];
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemHistoryField.Purged));
            Assert.That(entry.AccountId, Is.Null);
            Assert.That(entry.ApiKeyId, Is.Null);
        });
    }

    private async Task SetRetentionDaysAsync(int days)
    {
        var settings = await _db.ServerSettings.SingleAsync();
        settings.SetTrashRetention(days);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// A purge runs in its own request, so nothing it has to clear up is tracked; otherwise EF
    /// would null and cascade those rows itself and hide anything the purge forgot.
    /// </summary>
    private void InAFreshRequest() => _db.ChangeTracker.Clear();

    private void UseAgentCaller()
    {
        var agent = User.CreateAgent("Nightly triage", null);
        _db.Users.Add(agent);
        _db.SaveChanges();
        _caller.Current = new Caller(agent.Id, Guid.NewGuid(), null, ApiKeyScope.ReadWrite);
    }
}
