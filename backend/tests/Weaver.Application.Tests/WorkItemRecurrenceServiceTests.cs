using static Weaver.Application.Tests.Patches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemRecurrenceServiceTests
{
    // Saturday 26 Sep 2026, so the lead window runs to Saturday 3 Oct.
    private static readonly DateOnly Today = new(2026, 9, 26);
    private static readonly DateOnly Monday = new(2026, 9, 28);

    private WeaverDbContext _db = null!;
    private FixedTimeProvider _time = null!;
    private WorkItemRecurrenceService _recurrences = null!;
    private WorkItemService _workItems = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_time);
        var calendar = new ServerCalendar(new ServerSettingsService(_db, new AdminAuthorizer(_db)), _time);
        _recurrences = new WorkItemRecurrenceService(_db, calendar, NullLogger<WorkItemRecurrenceService>.Instance, FakeCallerContext.Anyone());
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), FakeCallerContext.Anyone());
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private Task<WorkItem> CreateItem(string title, Guid? parentId = null, Guid? statusId = null) =>
        _workItems.CreateAsync(title, null, parentId, statusId ?? _space.ToDo, spaceId: parentId is null ? _space.Id : null);

    private Task<RecurringWorkItem> RepeatWeekly(Guid id, params DayOfWeek[] days) =>
        _recurrences.SetAsync(id, RecurrenceFrequency.Weekly, days, Today, null);

    private Task<List<WorkItem>> Occurrences(Guid templateId) =>
        _db.WorkItems.Where(w => w.RecurrenceSourceId == templateId).OrderBy(w => w.RecurrenceDate).ToListAsync();

    [Test]
    public async Task SetAsync_StoresTheScheduleAndReportsTheNextOccurrence()
    {
        var item = await CreateItem("Report");

        var result = await RepeatWeekly(item.Id, DayOfWeek.Wednesday);

        Assert.That(result.Schedule.Frequency, Is.EqualTo(RecurrenceFrequency.Weekly));
        Assert.That(result.Schedule.Days, Is.EqualTo(RecurrenceDays.Wednesday));
        Assert.That(result.WorkItemTitle, Is.EqualTo("Report"));
        Assert.That(result.NextOccurrence, Is.EqualTo(new DateOnly(2026, 9, 30)));
    }

    [Test]
    public async Task SetAsync_AndListAsync_LoadTheParentForDisplay()
    {
        var lane = await CreateItem("Lane");
        var item = await CreateItem("Report", lane.Id);

        var set = await RepeatWeekly(item.Id, DayOfWeek.Monday);
        _db.ChangeTracker.Clear();
        var listed = (await _recurrences.ListAsync()).Single();
        var fetched = await _recurrences.GetAsync(item.Id);

        Assert.That(set.ParentTitle, Is.EqualTo("Lane"));
        Assert.That(listed.ParentTitle, Is.EqualTo("Lane"));
        Assert.That(fetched!.ParentTitle, Is.EqualTo("Lane"));
    }

    [Test]
    public void SetAsync_WithUnknownWorkItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => RepeatWeekly(Guid.NewGuid(), DayOfWeek.Monday));
    }

    [Test]
    public async Task SetAsync_WithInvalidSchedule_ThrowsAndStoresNothing()
    {
        var item = await CreateItem("Report");

        Assert.ThrowsAsync<DomainValidationException>(() => RepeatWeekly(item.Id));
        Assert.That(await _db.WorkItemRecurrences.AnyAsync(), Is.False);
    }

    [Test]
    public async Task SetAsync_OnAGeneratedOccurrence_ThrowsDomainValidationException()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);
        var occurrence = (await Occurrences(item.Id)).Single();

        Assert.ThrowsAsync<DomainValidationException>(() => RepeatWeekly(occurrence.Id, DayOfWeek.Monday));
    }

    [Test]
    public async Task SetAsync_CreatesOccurrencesWithinTheLeadWindowOnly()
    {
        var item = await CreateItem("Standup");

        await RepeatWeekly(item.Id, DayOfWeek.Monday, DayOfWeek.Friday);

        var dates = (await Occurrences(item.Id)).Select(w => w.RecurrenceDate);
        Assert.That(dates, Is.EqualTo(new DateOnly?[] { Monday, new DateOnly(2026, 10, 2) }));
    }

    [Test]
    public async Task SetAsync_CopiesTheTemplateIntoTheSameParentInTheFirstStatus()
    {
        var lane = await CreateItem("Lane");
        var item = await CreateItem("Report", lane.Id, _space.Doing);
        var user = User.CreateHuman("sam", "sam");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _workItems.UpdateDetailsAsync(item.Id, Details("Report", "Fill it in", _space.Task, _space.High));
        await _workItems.AssignAsync(item.Id, user.Id);
        await _workItems.SetTagsAsync(item.Id, Tags("finance"));
        await _workItems.RescheduleAsync(item.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5));

        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        var copy = (await Occurrences(item.Id)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(copy.Id, Is.Not.EqualTo(item.Id));
            Assert.That(copy.Title, Is.EqualTo("Report"));
            Assert.That(copy.Description, Is.EqualTo("Fill it in"));
            Assert.That(copy.TypeId, Is.EqualTo(_space.Task));
            Assert.That(copy.PriorityId, Is.EqualTo(_space.High));
            Assert.That(copy.AssignedToUserId, Is.EqualTo(user.Id));
            Assert.That(copy.Tags, Is.EqualTo(new[] { "finance" }));
            Assert.That(copy.ParentId, Is.EqualTo(lane.Id));
            Assert.That(copy.StatusId, Is.EqualTo(_space.ToDo));
            Assert.That(copy.StartDate, Is.EqualTo(Monday));
            Assert.That(copy.EndDate, Is.EqualTo(Monday));
        });
    }

    [Test]
    public async Task SetAsync_StartsOccurrencesInTheFirstStatusOfTheTemplatesOwnSpace()
    {
        // Ordered before every status of the template's Space, so only a Space-blind query picks it.
        var other = TestSpaces.Add(_db, "Village");
        _db.Statuses.Add(Status.Create(other.Id, "Backlog", -1, StatusCategory.ToDo, "#9E9E9E"));
        await _db.SaveChangesAsync();
        var item = await CreateItem("Report", statusId: _space.Doing);

        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        var copy = (await Occurrences(item.Id)).Single();
        Assert.That(copy.StatusId, Is.EqualTo(_space.ToDo));
    }

    [Test]
    public async Task SetAsync_DoesNotLeaveTheTemplateChanged()
    {
        var item = await CreateItem("Report", statusId: _space.Done);

        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        var reloaded = await _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
        Assert.That(reloaded.StatusId, Is.EqualTo(_space.Done));
        Assert.That(reloaded.RecurrenceSourceId, Is.Null);
    }

    [Test]
    public async Task SetAsync_SkipsTheOccurrenceOnTheTemplatesOwnStartDate()
    {
        var item = await CreateItem("Report");
        await _workItems.RescheduleAsync(item.Id, Monday, Monday);

        await RepeatWeekly(item.Id, DayOfWeek.Monday, DayOfWeek.Friday);

        var dates = (await Occurrences(item.Id)).Select(w => w.RecurrenceDate);
        Assert.That(dates, Is.EqualTo(new DateOnly?[] { new DateOnly(2026, 10, 2) }));
    }

    [Test]
    public async Task SetAsync_CopiesTheSubItemTreeInRankOrder()
    {
        var item = await CreateItem("Checklist");
        var first = await CreateItem("First", item.Id);
        var second = await _workItems.CreateAsync("Second", null, item.Id, _space.ToDo, afterId: first.Id);
        await CreateItem("Nested", second.Id, _space.Done);

        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        var copy = (await Occurrences(item.Id)).Single();
        var children = await _db.WorkItems.Where(w => w.ParentId == copy.Id).OrderBy(w => w.Rank).ToListAsync();
        Assert.That(children.Select(c => c.Title), Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(children.All(c => c.RecurrenceSourceId is null && c.StartDate == Monday), Is.True);
        var nested = await _db.WorkItems.SingleAsync(w => w.ParentId == children[1].Id);
        Assert.That(nested.Title, Is.EqualTo("Nested"));
        Assert.That(nested.StatusId, Is.EqualTo(_space.ToDo));
    }

    [Test]
    public async Task SetAsync_DoesNotCopySubItemsGeneratedByAnotherRepetition()
    {
        var item = await CreateItem("Checklist");
        var child = await CreateItem("Daily", item.Id);
        await RepeatWeekly(child.Id, DayOfWeek.Tuesday);

        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        var copy = (await Occurrences(item.Id)).Single();
        var copiedTitles = await _db.WorkItems.Where(w => w.ParentId == copy.Id).Select(w => w.Title).ToListAsync();
        Assert.That(copiedTitles, Is.EqualTo(new[] { "Daily" }));
    }

    [Test]
    public async Task SetAsync_AppendsOccurrencesToTheEndOfTheColumn()
    {
        var lane = await CreateItem("Lane");
        var existing = await CreateItem("Existing", lane.Id);
        var item = await CreateItem("Report", lane.Id, _space.Done);

        await RepeatWeekly(item.Id, DayOfWeek.Monday, DayOfWeek.Friday);

        var column = await _db.WorkItems
            .Where(w => w.ParentId == lane.Id && w.StatusId == _space.ToDo)
            .OrderBy(w => w.Rank)
            .Select(w => w.Id)
            .ToListAsync();
        var occurrenceIds = (await Occurrences(item.Id)).Select(w => w.Id);
        Assert.That(column, Is.EqualTo(new[] { existing.Id }.Concat(occurrenceIds)));
    }

    [Test]
    public async Task SetAsync_Again_DoesNotDuplicateOccurrences()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        Assert.That(await Occurrences(item.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SetAsync_WithANewSchedule_KeepsExistingOccurrencesAndAddsTheNewOnes()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        var result = await RepeatWeekly(item.Id, DayOfWeek.Thursday);

        var dates = (await Occurrences(item.Id)).Select(w => w.RecurrenceDate);
        Assert.That(dates, Is.EqualTo(new DateOnly?[] { Monday, new DateOnly(2026, 10, 1) }));
        Assert.That(result.Schedule.Days, Is.EqualTo(RecurrenceDays.Thursday));
        Assert.That(await _db.WorkItemRecurrences.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SetAsync_WithAFutureStart_CreatesNothingYet()
    {
        var item = await CreateItem("Report");

        var result = await _recurrences.SetAsync(
            item.Id, RecurrenceFrequency.Monthly, [], new DateOnly(2026, 11, 15), null);

        Assert.That(await Occurrences(item.Id), Is.Empty);
        Assert.That(result.NextOccurrence, Is.EqualTo(new DateOnly(2026, 11, 15)));
    }

    [Test]
    public async Task SetAsync_WithAPastStart_DoesNotBackfill()
    {
        var item = await CreateItem("Report");

        await _recurrences.SetAsync(item.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Monday], new DateOnly(2026, 1, 5), null);

        Assert.That((await Occurrences(item.Id)).Select(w => w.RecurrenceDate), Is.EqualTo(new DateOnly?[] { Monday }));
    }

    [Test]
    public async Task GenerateDueAsync_AsTimePasses_CreatesTheNextOccurrences()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        _time.Advance(TimeSpan.FromDays(2));
        var created = await _recurrences.GenerateDueAsync();

        Assert.That(created, Is.EqualTo(1));
        Assert.That(
            (await Occurrences(item.Id)).Select(w => w.RecurrenceDate),
            Is.EqualTo(new DateOnly?[] { Monday, Monday.AddDays(7) }));
    }

    [Test]
    public async Task GenerateDueAsync_RunTwice_CreatesNothingTheSecondTime()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);
        _time.Advance(TimeSpan.FromDays(2));
        await _recurrences.GenerateDueAsync();

        var created = await _recurrences.GenerateDueAsync();

        Assert.That(created, Is.Zero);
    }

    [Test]
    public async Task GenerateDueAsync_DoesNotRecreateADeletedOccurrence()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);
        var occurrence = (await Occurrences(item.Id)).Single();

        await _workItems.DeleteAsync(occurrence.Id);
        await _recurrences.GenerateDueAsync();

        Assert.That(await Occurrences(item.Id), Is.Empty);
    }

    [Test]
    public async Task GenerateDueAsync_CatchesUpOnOccurrencesMissedWhileStopped()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        _time.Advance(TimeSpan.FromDays(21));
        await _recurrences.GenerateDueAsync();

        Assert.That((await Occurrences(item.Id)).Select(w => w.RecurrenceDate), Is.EqualTo(new DateOnly?[]
        {
            Monday, Monday.AddDays(7), Monday.AddDays(14), Monday.AddDays(21),
        }));
    }

    [Test]
    public async Task GenerateDueAsync_StopsAtTheEndDate()
    {
        var item = await CreateItem("Report");
        await _recurrences.SetAsync(item.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Monday], Today, Monday.AddDays(7));

        _time.Advance(TimeSpan.FromDays(30));
        await _recurrences.GenerateDueAsync();

        Assert.That(
            (await Occurrences(item.Id)).Select(w => w.RecurrenceDate),
            Is.EqualTo(new DateOnly?[] { Monday, Monday.AddDays(7) }));
    }

    [Test]
    public async Task GenerateDueAsync_UsesTheTemplatesCurrentParent()
    {
        var oldLane = await CreateItem("Old lane");
        var newLane = await CreateItem("New lane");
        var item = await CreateItem("Report", oldLane.Id);
        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        await _workItems.ReparentAsync(item.Id, newLane.Id);
        _time.Advance(TimeSpan.FromDays(7));
        await _recurrences.GenerateDueAsync();

        var parents = (await Occurrences(item.Id)).Select(w => w.ParentId);
        Assert.That(parents, Is.EqualTo(new Guid?[] { oldLane.Id, newLane.Id }));
    }

    [Test]
    public async Task GenerateDueAsync_OneFailingRepetition_DoesNotStopTheOthers()
    {
        var broken = await CreateItem("Broken");
        var healthy = await CreateItem("Healthy");
        await RepeatWeekly(broken.Id, DayOfWeek.Monday);
        await RepeatWeekly(healthy.Id, DayOfWeek.Monday);
        // InMemory doesn't cascade to untracked rows, so this leaves a repetition whose
        // template is missing: a failure local to that one repetition.
        _db.ChangeTracker.Clear();
        _db.WorkItems.Remove(await _db.WorkItems.SingleAsync(w => w.Id == broken.Id));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _time.Advance(TimeSpan.FromDays(7));
        var created = await _recurrences.GenerateDueAsync();

        Assert.That(created, Is.EqualTo(1));
        Assert.That(await Occurrences(healthy.Id), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RemoveAsync_StopsRepeatingButKeepsGeneratedItems()
    {
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Monday);

        await _recurrences.RemoveAsync(item.Id);
        _time.Advance(TimeSpan.FromDays(7));
        await _recurrences.GenerateDueAsync();

        Assert.That(await _recurrences.GetAsync(item.Id), Is.Null);
        Assert.That(await Occurrences(item.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RemoveAsync_WhenNotRepeating_IsANoOp()
    {
        var item = await CreateItem("Report");

        Assert.DoesNotThrowAsync(() => _recurrences.RemoveAsync(item.Id));
    }

    [Test]
    public void RemoveAsync_WithUnknownWorkItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _recurrences.RemoveAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task GetAsync_WhenNotRepeating_ReturnsNull()
    {
        var item = await CreateItem("Report");

        Assert.That(await _recurrences.GetAsync(item.Id), Is.Null);
    }

    [Test]
    public void GetAsync_WithUnknownWorkItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _recurrences.GetAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task ListAsync_ReturnsEveryRepetitionByWorkItemNumber()
    {
        var first = await CreateItem("First");
        var second = await CreateItem("Second");
        // Number is a Postgres identity column the InMemory provider doesn't generate.
        _db.Entry(first).Property(w => w.Number).CurrentValue = 1;
        _db.Entry(second).Property(w => w.Number).CurrentValue = 2;
        await _db.SaveChangesAsync();
        await RepeatWeekly(second.Id, DayOfWeek.Monday);
        await _recurrences.SetAsync(first.Id, RecurrenceFrequency.Yearly, [], new DateOnly(2027, 1, 1), null);

        var list = await _recurrences.ListAsync();

        Assert.That(list.Select(r => r.WorkItemTitle), Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(list[0].NextOccurrence, Is.EqualTo(new DateOnly(2027, 1, 1)));
    }

    // Each pair straddles midnight in the server's zone: Tokyo is a day ahead of the UTC date
    // just after its midnight, New York a day behind just before its own. Saturday 26 Sep is
    // an occurrence; Sunday 27 Sep is not, so the next one is then Saturday 3 Oct.
    [TestCase("Asia/Tokyo", "2026-09-26T14:30:00Z", "2026-09-26")]
    [TestCase("Asia/Tokyo", "2026-09-26T15:30:00Z", "2026-10-03")]
    [TestCase("America/New_York", "2026-09-27T03:30:00Z", "2026-09-26")]
    [TestCase("America/New_York", "2026-09-27T04:30:00Z", "2026-10-03")]
    public async Task SetAsync_TakesTodayFromTheServerTimeZone(string timeZoneId, string now, string expectedFirst)
    {
        await UseServerTimeZone(timeZoneId);
        MoveClockTo(DateTimeOffset.Parse(now));
        var item = await CreateItem("Report");

        var result = await _recurrences.SetAsync(
            item.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Saturday], new DateOnly(2026, 9, 1), null);

        var first = DateOnly.Parse(expectedFirst);
        Assert.That(result.NextOccurrence, Is.EqualTo(first));
        Assert.That((await Occurrences(item.Id)).First().RecurrenceDate, Is.EqualTo(first));
    }

    [Test]
    public async Task GenerateDueAsync_UsesTheServerTimeZoneForTheLeadWindow()
    {
        await UseServerTimeZone("Asia/Tokyo");
        var item = await CreateItem("Report");
        await RepeatWeekly(item.Id, DayOfWeek.Saturday);
        // Friday 2 Oct 23:30 UTC is already Saturday 3 Oct in Tokyo, which brings Saturday
        // 10 Oct inside the lead window that a UTC "today" would still leave out.
        MoveClockTo(new DateTimeOffset(2026, 10, 2, 23, 30, 0, TimeSpan.Zero));

        await _recurrences.GenerateDueAsync();

        Assert.That(
            (await Occurrences(item.Id)).Select(w => w.RecurrenceDate),
            Is.EqualTo(new DateOnly?[] { Today, Today.AddDays(7), Today.AddDays(14) }));
    }

    private async Task UseServerTimeZone(string timeZoneId)
    {
        var settings = await _db.ServerSettings.SingleAsync();
        settings.SetTimeZone(timeZoneId);
        await _db.SaveChangesAsync();
    }

    private void MoveClockTo(DateTimeOffset now) => _time.Advance(now - _time.GetUtcNow());
}
