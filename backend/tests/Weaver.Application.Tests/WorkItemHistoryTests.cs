using static Weaver.Application.Tests.Patches;
using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Application.Tests;

/// <summary>
/// What each kind of work item write records in its history, read back through
/// <see cref="IWorkItemHistoryService"/>.
/// </summary>
[TestFixture]
public class WorkItemHistoryTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemHistoryService _history = null!;
    private User _alice = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _alice = TestUsers.Add(_db, "alice");
        _caller = new FakeCallerContext(_alice.Id);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), _caller);
        _history = new WorkItemHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Create_RecordsWhoCreatedTheItemAndItsTitle()
    {
        var item = await CreateAsync("Write the tests");

        var entry = (await _history.ListForWorkItemAsync(item.Id)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemHistoryField.Created));
            Assert.That(entry.AccountId, Is.EqualTo(_alice.Id));
            Assert.That(entry.AccountName, Is.EqualTo("alice"));
            Assert.That(entry.ChangedAtUtc, Is.EqualTo(_clock.GetUtcNow()));
            Assert.That(entry.OldValue, Is.Null);
            Assert.That(entry.NewValue?.GetString(), Is.EqualTo("Write the tests"));
        });
    }

    [Test]
    public async Task Write_RecordsTheApiKeyAndTrimmedActorLabelTheCallerSent()
    {
        var keyId = Guid.NewGuid();
        _caller.Current = new Caller(_alice.Id, keyId, "  sub-agent: test-writer ");

        var item = await CreateAsync("Task");

        var entry = (await _history.ListForWorkItemAsync(item.Id)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.ApiKeyId, Is.EqualTo(keyId));
            Assert.That(entry.ActorLabel, Is.EqualTo("sub-agent: test-writer"));
        });
    }

    [Test]
    public async Task UpdateDetails_RecordsEachChangedFieldWithItsOldAndNewValue()
    {
        var item = await CreateAsync("Task");

        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Renamed", Description = "Notes" });

        var entries = await ChangesAsync(item.Id);
        Assert.That(entries, Is.EqualTo(new[]
        {
            (WorkItemField.Description, (string?)null, (string?)"\"Notes\""),
            (WorkItemField.Title, "\"Task\"", "\"Renamed\""),
        }));
    }

    [Test]
    public async Task Write_ThatChangesNothing_RecordsNothing()
    {
        var item = await CreateAsync("Task");

        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Task" });
        await _workItems.SetTagsAsync(item.Id, Tags());

        Assert.That(await ChangesAsync(item.Id), Is.Empty);
    }

    [Test]
    public async Task Write_ThatFails_RecordsNothing()
    {
        var item = await CreateAsync("Task");

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() => _workItems.UpdateDetailsAsync(
            item.Id, new WorkItemDetailsPatch { Title = "Renamed" }, expectedVersion: item.Version + 1));
        Assert.ThrowsAsync<DomainValidationException>(() => _workItems.UpdateDetailsAsync(
            item.Id, new WorkItemDetailsPatch { Title = "  " }));

        Assert.That(await ChangesAsync(item.Id), Is.Empty);
    }

    [Test]
    public async Task ChangeStatus_RecordsTheStatusAndNamesIt_ButNotTheRank()
    {
        var item = await CreateAsync("Task");
        var sibling = await CreateAsync("Sibling");

        await _workItems.ChangeStatusAsync(item.Id, StatusConfiguration.DoingId);
        await _workItems.ChangeStatusAsync(item.Id, StatusConfiguration.DoingId, afterId: null);
        await _workItems.ChangeStatusAsync(sibling.Id, StatusConfiguration.ToDoId, afterId: null);

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(async () =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemField.StatusId));
            Assert.That(entry.OldValue?.GetGuid(), Is.EqualTo(StatusConfiguration.ToDoId));
            Assert.That(entry.NewValue?.GetGuid(), Is.EqualTo(StatusConfiguration.DoingId));
            Assert.That(entry.OldName, Is.EqualTo("To Do"));
            Assert.That(entry.NewName, Is.EqualTo("Doing"));
            Assert.That(await ChangesAsync(item.Id), Has.Count.EqualTo(1));
            Assert.That(await ChangesAsync(sibling.Id), Is.Empty);
        });
    }

    [Test]
    public async Task Reparent_RecordsTheParentNamedByNumberAndTitle()
    {
        var parent = await CreateAsync("Parent");
        var item = await CreateAsync("Task");

        await _workItems.ReparentAsync(item.Id, parent.Id);

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemField.ParentId));
            Assert.That(entry.OldValue, Is.Null);
            Assert.That(entry.NewValue?.GetGuid(), Is.EqualTo(parent.Id));
            Assert.That(entry.NewName, Is.EqualTo($"#{parent.Number} Parent"));
        });
    }

    [Test]
    public async Task RescheduleAssignAndSetTags_RecordTheirFields()
    {
        var item = await CreateAsync("Task");

        await _workItems.RescheduleAsync(item.Id, new DateOnly(2026, 9, 3), Optional<DateOnly?>.Unset);
        await _workItems.AssignAsync(item.Id, _alice.Id);
        await _workItems.SetTagsAsync(item.Id, Tags("urgent", "later"));

        var entries = await _history.ListForWorkItemAsync(item.Id);
        Assert.Multiple(() =>
        {
            Assert.That(entries.Take(3).Select(e => e.Field), Is.EqualTo(new[]
            {
                WorkItemField.Tags, WorkItemField.AssignedToUserId, WorkItemField.StartDate,
            }));
            Assert.That(entries[0].OldValue?.GetRawText(), Is.EqualTo("[]"));
            Assert.That(entries[0].NewValue?.GetRawText(), Is.EqualTo("[\"urgent\",\"later\"]"));
            Assert.That(entries[1].NewName, Is.EqualTo("alice"));
            Assert.That(entries[2].NewValue?.GetString(), Is.EqualTo("2026-09-03"));
        });
    }

    [Test]
    public async Task Delete_RecordsADeleteForTheItemAndEachDescendant_ReadableAfterwards()
    {
        var root = await CreateAsync("Root");
        var child = await CreateAsync("Child", root.Id);

        await _workItems.DeleteAsync(root.Id, cascade: true);

        var rootEntry = (await _history.ListForWorkItemAsync(root.Id)).First();
        var childEntry = (await _history.ListForWorkItemAsync(child.Id)).First();
        Assert.Multiple(() =>
        {
            Assert.That(rootEntry.Field, Is.EqualTo(WorkItemHistoryField.Deleted));
            Assert.That(rootEntry.OldValue?.GetString(), Is.EqualTo("Root"));
            Assert.That(rootEntry.NewValue, Is.Null);
            Assert.That(childEntry.Field, Is.EqualTo(WorkItemHistoryField.Deleted));
        });
    }

    [Test]
    public async Task Comments_RecordAddEditAndDeleteWithTheBody()
    {
        var comments = new CommentService(_db, _clock, _caller);
        var item = await CreateAsync("Task");

        var comment = await comments.CreateAsync(item.Id, "First draft");
        await comments.UpdateAsync(comment.Id, "Second draft");
        await comments.DeleteAsync(comment.Id);

        Assert.That(await ChangesAsync(item.Id), Is.EqualTo(new[]
        {
            (WorkItemHistoryField.Comment, (string?)"\"Second draft\"", (string?)null),
            (WorkItemHistoryField.Comment, "\"First draft\"", "\"Second draft\""),
            (WorkItemHistoryField.Comment, null, "\"First draft\""),
        }));
    }

    [Test]
    public async Task Comment_ThatFails_RecordsNothing()
    {
        var comments = new CommentService(_db, _clock, _caller);
        var item = await CreateAsync("Task");
        var comment = await comments.CreateAsync(item.Id, "Mine");
        _caller.Current = new Caller(Guid.NewGuid(), null, null);

        Assert.ThrowsAsync<CommentAuthorMismatchException>(() => comments.UpdateAsync(comment.Id, "Not yours"));
        Assert.ThrowsAsync<CommentAuthorMismatchException>(() => comments.DeleteAsync(comment.Id));

        Assert.That(await ChangesAsync(item.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Links_RecordAddAndRemoveOnBothItems_NamingTheOtherSide()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");

        var link = await links.CreateAsync(item.Id, other.Id);
        await links.DeleteAsync(link.Id);

        var itemEntries = (await _history.ListForWorkItemAsync(item.Id)).Take(2).ToList();
        var otherEntries = (await _history.ListForWorkItemAsync(other.Id)).Take(2).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(itemEntries.Select(e => e.Field), Is.All.EqualTo(WorkItemHistoryField.Link));
            Assert.That(itemEntries[0].OldValue?.GetGuid(), Is.EqualTo(other.Id));
            Assert.That(itemEntries[0].NewValue, Is.Null);
            Assert.That(itemEntries[1].NewValue?.GetGuid(), Is.EqualTo(other.Id));
            Assert.That(itemEntries[1].NewName, Is.EqualTo($"#{other.Number} Other"));
            Assert.That(otherEntries[1].NewValue?.GetGuid(), Is.EqualTo(item.Id));
            Assert.That(otherEntries[0].OldValue?.GetGuid(), Is.EqualTo(item.Id));
        });
    }

    [Test]
    public async Task Delete_RecordsTheLinkRemovedOnTheItemThatSurvives()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var item = await CreateAsync("Item");
        var survivor = await CreateAsync("Survivor");
        await links.CreateAsync(survivor.Id, item.Id);

        await _workItems.DeleteAsync(item.Id);

        var entry = (await _history.ListForWorkItemAsync(survivor.Id)).First();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemHistoryField.Link));
            Assert.That(entry.OldValue?.GetGuid(), Is.EqualTo(item.Id));
            Assert.That(entry.NewValue, Is.Null);
        });
    }

    [Test]
    public async Task Link_ThatFails_RecordsNothing()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");
        await links.CreateAsync(item.Id, other.Id);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => links.CreateAsync(other.Id, item.Id));

        Assert.That(await ChangesAsync(item.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Recurrence_RecordsStartingChangingAndStoppingWithTheSchedule()
    {
        var recurrences = RecurrenceService();
        var item = await CreateAsync("Report");
        var start = new DateOnly(2027, 1, 4);

        await recurrences.SetAsync(item.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Monday], start, null);
        await recurrences.SetAsync(item.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Monday], start, null);
        await recurrences.SetAsync(item.Id, RecurrenceFrequency.Monthly, [], start, null);
        await recurrences.RemoveAsync(item.Id);

        Assert.That(await ChangesAsync(item.Id), Is.EqualTo(new[]
        {
            (WorkItemHistoryField.Recurrence, (string?)MonthlyJson, (string?)null),
            (WorkItemHistoryField.Recurrence, WeeklyJson, MonthlyJson),
            (WorkItemHistoryField.Recurrence, null, WeeklyJson),
        }));
    }

    [Test]
    public async Task Recurrence_OccurrencesGeneratedInTheBackground_AreCreatedByTheSystem()
    {
        var recurrences = RecurrenceService();
        var item = await CreateAsync("Report");
        await recurrences.SetAsync(item.Id, RecurrenceFrequency.Weekly, [DayOfWeek.Monday], new DateOnly(2026, 9, 1), null);
        var setUpOccurrence = _db.WorkItems.Single(w => w.RecurrenceSourceId == item.Id);
        _clock.Advance(TimeSpan.FromDays(7));

        await recurrences.GenerateDueAsync();

        var generated = _db.WorkItems.Single(w => w.RecurrenceSourceId == item.Id && w.Id != setUpOccurrence.Id);
        var byCaller = (await _history.ListForWorkItemAsync(setUpOccurrence.Id)).Single();
        var bySystem = (await _history.ListForWorkItemAsync(generated.Id)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(byCaller.Field, Is.EqualTo(WorkItemHistoryField.Created));
            Assert.That(byCaller.AccountId, Is.EqualTo(_alice.Id));
            Assert.That(bySystem.Field, Is.EqualTo(WorkItemHistoryField.Created));
            Assert.That(bySystem.AccountId, Is.Null);
            Assert.That(bySystem.NewValue?.GetString(), Is.EqualTo("Report"));
        });
    }

    [Test]
    public void List_ForAnItemThatNeverExisted_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _history.ListForWorkItemAsync(Guid.NewGuid()));
    }

    private const string WeeklyJson =
        "{\"frequency\":\"Weekly\",\"daysOfWeek\":[\"Monday\"],\"startDate\":\"2027-01-04\",\"endDate\":null}";

    private const string MonthlyJson =
        "{\"frequency\":\"Monthly\",\"daysOfWeek\":[],\"startDate\":\"2027-01-04\",\"endDate\":null}";

    private WorkItemRecurrenceService RecurrenceService() => new(
        _db,
        new ServerCalendar(new ServerSettingsService(_db, new AdminAuthorizer(_db)), _clock),
        NullLogger<WorkItemRecurrenceService>.Instance,
        _caller);

    private Task<WorkItem> CreateAsync(string title, Guid? parentId = null) =>
        _workItems.CreateAsync(title, null, parentId, StatusConfiguration.ToDoId);

    /// <summary>The entries after "created", newest first, as (field, old JSON, new JSON).</summary>
    private async Task<IReadOnlyList<(string Field, string? Old, string? New)>> ChangesAsync(Guid workItemId) =>
        (await _history.ListForWorkItemAsync(workItemId))
        .Where(e => e.Field != WorkItemHistoryField.Created)
        .Select(e => (e.Field, e.OldValue?.GetRawText(), e.NewValue?.GetRawText()))
        .ToList();
}
