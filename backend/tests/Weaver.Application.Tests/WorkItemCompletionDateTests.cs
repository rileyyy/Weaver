using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Every service that changes an item's status, or what its status means, keeps the completion
/// date and records it in history, taking "today" from the server calendar.
/// </summary>
[TestFixture]
public class WorkItemCompletionDateTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateOnly Friday = new(2026, 11, 13);

    private WeaverDbContext _db = null!;
    private FixedServerCalendar _calendar = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private StatusService _statuses = null!;
    private WorkItemHistoryService _history = null!;
    private User _admin = null!;
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(clock);
        _calendar = new FixedServerCalendar { Today = Monday };
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _caller = new FakeCallerContext(_admin.Id);
        _home = TestSpaces.Add(_db, "Home");
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), _calendar, _caller);
        _statuses = new StatusService(_db, new AdminAuthorizer(_db), _calendar, _caller);
        _history = new WorkItemHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private Task<WorkItem> CreateAsync(Guid statusId, Guid? parentId = null) =>
        _workItems.CreateAsync("Task", null, parentId, statusId, spaceId: parentId is null ? _home.Id : null);

    private Task<WorkItem> ReloadAsync(Guid id) =>
        _db.WorkItems.IgnoreQueryFilters().AsNoTracking().SingleAsync(w => w.Id == id);

    private async Task<List<WorkItemHistoryView>> CompletionEntriesAsync(Guid id) =>
        (await _history.ListForWorkItemAsync(id)).Where(e => e.Field == WorkItemField.CompletionDate).ToList();

    private static DateOnly? DateIn(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } v ? DateOnly.Parse(v.GetString()!) : null;

    [Test]
    public async Task CreateAsync_InADoneStatus_IsCompletedOnTheServersToday()
    {
        var item = await CreateAsync(_home.Done);

        Assert.That((await ReloadAsync(item.Id)).CompletionDate, Is.EqualTo(Monday));
    }

    [Test]
    public async Task ChangeStatusAsync_IntoDone_SetsTodayReportsItAndRecordsIt()
    {
        var item = await CreateAsync(_home.Doing);

        var result = await _workItems.ChangeStatusAsync(item.Id, _home.Done);

        var entries = await CompletionEntriesAsync(item.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.Item.CompletionDate, Is.EqualTo(Monday));
            Assert.That(result.ChangedFields, Does.Contain(WorkItemField.CompletionDate));
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(DateIn(entries[0].OldValue), Is.Null);
            Assert.That(DateIn(entries[0].NewValue), Is.EqualTo(Monday));
        });
    }

    [Test]
    public async Task ChangeStatusAsync_OutOfDone_ClearsTheDateAndRecordsIt()
    {
        var item = await CreateAsync(_home.Done);
        _calendar.Today = Friday;

        var result = await _workItems.ChangeStatusAsync(item.Id, _home.ToDo);

        var entries = await CompletionEntriesAsync(item.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.Item.CompletionDate, Is.Null);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(DateIn(entries[0].OldValue), Is.EqualTo(Monday));
            Assert.That(DateIn(entries[0].NewValue), Is.Null);
        });
    }

    [Test]
    public async Task ChangeStatusAsync_BetweenDoneStatuses_KeepsTheOriginalDate()
    {
        var archived = await _statuses.CreateAsync(_admin.Id, _home.Id, "Archived", StatusCategory.Done, "#616161");
        var item = await CreateAsync(_home.Done);
        _calendar.Today = Friday;

        var result = await _workItems.ChangeStatusAsync(item.Id, archived.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Item.CompletionDate, Is.EqualTo(Monday));
            Assert.That(result.ChangedFields, Does.Not.Contain(WorkItemField.CompletionDate));
        });
    }

    [Test]
    public async Task UpdateAsync_RecategorizingAStatusIntoDone_CompletesItsItemsIncludingTrashedOnes()
    {
        var review = await _statuses.CreateAsync(_admin.Id, _home.Id, "Review", StatusCategory.Doing, "#00897B");
        var live = await CreateAsync(review.Id);
        var trashed = await CreateAsync(review.Id);
        await new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), _caller, TimeProvider.System)
            .MoveToTrashAsync(trashed.Id);
        var elsewhere = await CreateAsync(_home.Doing);
        _calendar.Today = Friday;

        await _statuses.UpdateAsync(_admin.Id, review.Id, "Review", StatusCategory.Done, "#00897B");

        Assert.Multiple(async () =>
        {
            Assert.That((await ReloadAsync(live.Id)).CompletionDate, Is.EqualTo(Friday));
            Assert.That((await ReloadAsync(trashed.Id)).CompletionDate, Is.EqualTo(Friday));
            Assert.That((await ReloadAsync(elsewhere.Id)).CompletionDate, Is.Null);
            var entries = await CompletionEntriesAsync(live.Id);
            Assert.That(entries.Select(e => DateIn(e.NewValue)), Is.EqualTo(new DateOnly?[] { Friday }));
            Assert.That(entries.Select(e => e.AccountId), Is.All.EqualTo(_admin.Id));
        });
    }

    [Test]
    public async Task UpdateAsync_RecategorizingAStatusOutOfDone_ReopensItsItems()
    {
        var item = await CreateAsync(_home.Done);

        await _statuses.UpdateAsync(_admin.Id, _home.Done, "Done", StatusCategory.Doing, "#7B1FA2");

        var entries = await CompletionEntriesAsync(item.Id);
        Assert.Multiple(async () =>
        {
            Assert.That((await ReloadAsync(item.Id)).CompletionDate, Is.Null);
            Assert.That(entries.Select(e => (DateIn(e.OldValue), DateIn(e.NewValue))), Is.EqualTo(new[] { ((DateOnly?)Monday, (DateOnly?)null) }));
        });
    }

    [Test]
    public async Task UpdateAsync_KeepingTheCategory_LeavesCompletionDatesAlone()
    {
        var item = await CreateAsync(_home.Done);
        _calendar.Today = Friday;

        await _statuses.UpdateAsync(_admin.Id, _home.Done, "Finished", StatusCategory.Done, "#123456");

        Assert.Multiple(async () =>
        {
            Assert.That((await ReloadAsync(item.Id)).CompletionDate, Is.EqualTo(Monday));
            Assert.That(await CompletionEntriesAsync(item.Id), Is.Empty);
        });
    }

    [Test]
    public async Task DeleteAsync_WithADoneReplacement_CompletesTheMovedItems()
    {
        var item = await CreateAsync(_home.Doing);
        _calendar.Today = Friday;

        await _statuses.DeleteAsync(_admin.Id, _home.Doing, _home.Done);

        Assert.That((await ReloadAsync(item.Id)).CompletionDate, Is.EqualTo(Friday));
    }

    [Test]
    public async Task MoveAsync_IntoAnotherSpacesDoneStatus_CompletesTheItemAndItsChildren()
    {
        var workshop = TestWorkshop.Add(_db);
        var parent = await CreateAsync(_home.Doing);
        var child = await CreateAsync(_home.Done, parent.Id);
        _calendar.Today = Friday;
        var moves = new WorkItemSpaceMoveService(_db, new IterativeWorkItemHierarchy(_db), _calendar, _caller);
        var maps = new SpaceMoveMaps(
            new Dictionary<Guid, Guid> { [_home.Doing] = workshop.Done, [_home.Done] = workshop.Booked },
            new Dictionary<Guid, Guid?>(),
            new Dictionary<Guid, Guid?>());

        var result = await moves.MoveAsync(parent.Id, workshop.Id, null, maps);

        Assert.Multiple(async () =>
        {
            Assert.That(result.ChangedFields, Does.Contain(WorkItemField.CompletionDate));
            Assert.That((await ReloadAsync(parent.Id)).CompletionDate, Is.EqualTo(Friday));
            Assert.That((await ReloadAsync(child.Id)).CompletionDate, Is.Null);
            Assert.That(await CompletionEntriesAsync(child.Id), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Occurrences_OfADoneTemplate_StartWithoutACompletionDate()
    {
        var template = await CreateAsync(_home.Done);
        var recurrences = new WorkItemRecurrenceService(
            _db, _calendar, NullLogger<WorkItemRecurrenceService>.Instance, _caller);

        await recurrences.SetAsync(template.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Wednesday], Monday, null);

        var copies = await _db.WorkItems.AsNoTracking().Where(w => w.RecurrenceSourceId == template.Id).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(copies, Is.Not.Empty);
            Assert.That(copies.Select(c => c.CompletionDate), Is.All.Null);
        });
    }
}
