using static Weaver.Application.Tests.Patches;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

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
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _alice = TestUsers.Add(_db, "alice");
        _home = TestSpaces.Add(_db, "Home");
        _caller = new FakeCallerContext(_alice.Id);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), _caller);
        _history = new WorkItemHistoryService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Create_RecordsWhoCreatedTheItemAndItsTitle()
    {
        var item = await CreateAsync("Write the tests");

        var entry = await CreatedEntryAsync(item.Id);

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
    public async Task Create_RecordsTheItemsSpace_NamedForReaders()
    {
        var item = await CreateAsync("Write the tests");
        var child = await CreateAsync("Sub-task", item.Id);

        var entries = await _history.ListForWorkItemAsync(child.Id);

        var space = entries.Single(e => e.Field == WorkItemField.SpaceId);
        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => e.Field), Is.EqualTo(new[] { WorkItemField.SpaceId, WorkItemHistoryField.Created }));
            Assert.That(space.OldValue, Is.Null);
            Assert.That(space.NewValue?.GetGuid(), Is.EqualTo(_home.Id));
            Assert.That(space.NewName, Is.EqualTo("Home"));
            Assert.That(space.AccountId, Is.EqualTo(_alice.Id));
        });
    }

    [Test]
    public async Task Write_RecordsTheApiKeyAndTrimmedActorLabelTheCallerSent()
    {
        var keyId = Guid.NewGuid();
        _caller.Current = new Caller(_alice.Id, keyId, "  sub-agent: test-writer ");

        var item = await CreateAsync("Task");

        var entry = await CreatedEntryAsync(item.Id);
        Assert.Multiple(() =>
        {
            Assert.That(entry.ApiKeyId, Is.EqualTo(keyId));
            Assert.That(entry.ActorLabel, Is.EqualTo("sub-agent: test-writer"));
        });
    }

    [Test]
    public async Task Write_RecordsTheRunIdTheCallerSent()
    {
        var item = await CreateAsync("Task");
        _caller.Current = new Caller(_alice.Id, Guid.NewGuid(), ActorLabel: null, ApiKeyScope.ReadWrite, " run-1 ");

        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Renamed" });

        var entries = await _history.ListForWorkItemAsync(item.Id);
        Assert.That(entries.Select(e => (e.Field, e.RunId)), Is.EqualTo(new[]
        {
            (WorkItemField.Title, (string?)"run-1"),
            (WorkItemField.SpaceId, null),
            (WorkItemHistoryField.Created, null),
        }));
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
    public async Task UpdateDetails_RecordsTheWhenAsTypedAndWhenCleared()
    {
        var item = await CreateAsync("Task");

        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { When = "after drywall" });
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { When = null });

        var entries = await ChangesAsync(item.Id);
        Assert.That(entries, Is.EquivalentTo(new[]
        {
            (WorkItemField.When, (string?)null, (string?)"\"after drywall\""),
            (WorkItemField.When, "\"after drywall\"", (string?)null),
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

        await _workItems.ChangeStatusAsync(item.Id, _home.Doing);
        await _workItems.ChangeStatusAsync(item.Id, _home.Doing, afterId: null);
        await _workItems.ChangeStatusAsync(sibling.Id, _home.ToDo, afterId: null);

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(async () =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemField.StatusId));
            Assert.That(entry.OldValue?.GetGuid(), Is.EqualTo(_home.ToDo));
            Assert.That(entry.NewValue?.GetGuid(), Is.EqualTo(_home.Doing));
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
    public async Task Links_RecordAddAndRemoveOnBothItems_WithTheOtherSideAndTheTypeFromEachSide()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");

        var link = await links.CreateAsync(item.Id, other.Id, WorkItemLinkRelation.Blocks);
        await links.DeleteAsync(link.Id);

        var itemBlocks = $"{{\"workItemId\":\"{other.Id}\",\"type\":\"Blocks\"}}";
        var otherBlockedBy = $"{{\"workItemId\":\"{item.Id}\",\"type\":\"BlockedBy\"}}";
        Assert.Multiple(async () =>
        {
            Assert.That(await ChangesAsync(item.Id), Is.EqualTo(new[]
            {
                (WorkItemHistoryField.Link, (string?)itemBlocks, (string?)null),
                (WorkItemHistoryField.Link, null, itemBlocks),
            }));
            Assert.That(await ChangesAsync(other.Id), Is.EqualTo(new[]
            {
                (WorkItemHistoryField.Link, (string?)otherBlockedBy, (string?)null),
                (WorkItemHistoryField.Link, null, otherBlockedBy),
            }));
        });
    }

    [Test]
    public async Task Links_AreNamedAfterTheOtherItem()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");

        var link = await links.CreateAsync(item.Id, other.Id, WorkItemLinkRelation.Related);
        await links.DeleteAsync(link.Id);

        var entries = (await _history.ListForWorkItemAsync(item.Id)).Take(2).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(entries[0].OldName, Is.EqualTo($"#{other.Number} Other"));
            Assert.That(entries[1].NewName, Is.EqualTo($"#{other.Number} Other"));
        });
    }

    [Test]
    public async Task Links_RecordedBeforeTypes_AsAPlainId_StillReadAndAreNamed()
    {
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");
        _db.WorkItemHistory.Add(WorkItemHistoryEntry.Record(
            item.Id, _caller.Current.HistoryActor, WorkItemHistoryField.Link, null, $"\"{other.Id}\""));
        await _db.SaveChangesAsync();

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First(e => e.Field == WorkItemHistoryField.Link);

        Assert.Multiple(() =>
        {
            Assert.That(entry.NewValue?.GetGuid(), Is.EqualTo(other.Id));
            Assert.That(entry.NewName, Is.EqualTo($"#{other.Number} Other"));
        });
    }

    [Test]
    public async Task Link_ThatFails_RecordsNothing()
    {
        var links = new WorkItemLinkService(_db, _caller);
        var item = await CreateAsync("Item");
        var other = await CreateAsync("Other");
        await links.CreateAsync(item.Id, other.Id, WorkItemLinkRelation.Blocks);

        Assert.ThrowsAsync<BlockingCycleException>(() => links.CreateAsync(other.Id, item.Id, WorkItemLinkRelation.Blocks));
        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => links.CreateAsync(other.Id, item.Id, WorkItemLinkRelation.Related));

        Assert.That(await ChangesAsync(item.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UrlLinks_RecordAddAndRemoveWithTheUrlAndLabel_NamedByTheLabel()
    {
        var urlLinks = new WorkItemUrlLinkService(_db, _caller);
        var item = await CreateAsync("Item");

        var link = await urlLinks.CreateAsync(item.Id, "https://github.com/pr/1", "The PR");
        await urlLinks.DeleteAsync(link.Id);

        const string Value = "{\"url\":\"https://github.com/pr/1\",\"label\":\"The PR\"}";
        var entries = (await _history.ListForWorkItemAsync(item.Id)).Take(2).ToList();
        Assert.Multiple(async () =>
        {
            Assert.That(await ChangesAsync(item.Id), Is.EqualTo(new[]
            {
                (WorkItemHistoryField.UrlLink, (string?)Value, (string?)null),
                (WorkItemHistoryField.UrlLink, null, Value),
            }));
            Assert.That(entries[0].OldName, Is.EqualTo("The PR"));
            Assert.That(entries[1].NewName, Is.EqualTo("The PR"));
        });
    }

    [Test]
    public async Task UrlLink_WithoutALabel_IsNamedByItsUrl()
    {
        var urlLinks = new WorkItemUrlLinkService(_db, _caller);
        var item = await CreateAsync("Item");

        await urlLinks.CreateAsync(item.Id, "https://example.com/quote", null);

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(() =>
        {
            Assert.That(entry.NewValue?.GetRawText(), Is.EqualTo("{\"url\":\"https://example.com/quote\",\"label\":null}"));
            Assert.That(entry.NewName, Is.EqualTo("https://example.com/quote"));
        });
    }

    [Test]
    public async Task UrlLink_ThatFails_RecordsNothing()
    {
        var urlLinks = new WorkItemUrlLinkService(_db, _caller);
        var item = await CreateAsync("Item");

        Assert.ThrowsAsync<DomainValidationException>(() => urlLinks.CreateAsync(item.Id, "not a url", null));

        Assert.That(await ChangesAsync(item.Id), Is.Empty);
    }

    [Test]
    public async Task Attachments_RecordAddAndRemoveWithTheIdAndFileName_NamedByTheFileName()
    {
        var attachments = AttachmentService();
        var item = await CreateAsync("Item");

        var added = await attachments.AddAsync(item.Id, "report.pdf", "application/pdf", new MemoryStream([1, 2, 3]));
        await attachments.DeleteAsync(added.Id);

        var value = $"{{\"id\":\"{added.Id}\",\"fileName\":\"report.pdf\"}}";
        var entries = (await _history.ListForWorkItemAsync(item.Id)).Take(2).ToList();
        Assert.Multiple(async () =>
        {
            Assert.That(await ChangesAsync(item.Id), Is.EqualTo(new[]
            {
                (WorkItemHistoryField.Attachment, (string?)value, (string?)null),
                (WorkItemHistoryField.Attachment, null, value),
            }));
            Assert.That(entries[0].OldName, Is.EqualTo("report.pdf"));
            Assert.That(entries[1].NewName, Is.EqualTo("report.pdf"));
        });
    }

    [Test]
    public async Task Attachment_ThatFails_RecordsNothing()
    {
        var attachments = AttachmentService();
        var item = await CreateAsync("Item");

        Assert.ThrowsAsync<DomainValidationException>(() =>
            attachments.AddAsync(item.Id, "empty.txt", "text/plain", new MemoryStream()));

        Assert.That(await ChangesAsync(item.Id), Is.Empty);
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
        var byCaller = await CreatedEntryAsync(setUpOccurrence.Id);
        var bySystem = await CreatedEntryAsync(generated.Id);
        var systemSpace = (await _history.ListForWorkItemAsync(generated.Id)).Single(e => e.Field == WorkItemField.SpaceId);
        Assert.Multiple(() =>
        {
            Assert.That(byCaller.Field, Is.EqualTo(WorkItemHistoryField.Created));
            Assert.That(byCaller.AccountId, Is.EqualTo(_alice.Id));
            Assert.That(bySystem.Field, Is.EqualTo(WorkItemHistoryField.Created));
            Assert.That(bySystem.AccountId, Is.Null);
            Assert.That(bySystem.NewValue?.GetString(), Is.EqualTo("Report"));
            Assert.That(systemSpace.AccountId, Is.Null);
            Assert.That(systemSpace.NewValue?.GetGuid(), Is.EqualTo(_home.Id));
        });
    }

    [Test]
    public async Task DeletingALookupValue_RecordsTheMoveOnEachItemUsingIt()
    {
        var admin = SignInAdmin();
        var item = await CreateAsync("Task");
        await _workItems.ChangeStatusAsync(item.Id, _home.Doing);
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch
        {
            TypeId = _home.Task,
            PriorityId = _home.Low,
        });

        await new StatusService(_db, new AdminAuthorizer(_db), new FixedServerCalendar(), _caller).DeleteAsync(admin.Id, _home.Doing, _home.Done);
        await new WorkItemTypeService(_db, new AdminAuthorizer(_db), _caller).DeleteAsync(admin.Id, _home.Task, null);
        await new WorkItemPriorityService(_db, new AdminAuthorizer(_db), _caller).DeleteAsync(admin.Id, _home.Low, _home.High);

        // The replacement status is a Done one, so the status move also completes the item.
        var entries = (await _history.ListForWorkItemAsync(item.Id)).Take(4).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => e.Field), Is.EqualTo(new[]
            {
                WorkItemField.PriorityId, WorkItemField.TypeId, WorkItemField.CompletionDate, WorkItemField.StatusId,
            }));
            Assert.That(entries.Select(e => e.AccountId), Is.All.EqualTo(admin.Id));
            Assert.That(entries[0].NewName, Is.EqualTo("High"));
            Assert.That(entries[1].NewValue, Is.Null);
            Assert.That(entries[3].NewName, Is.EqualTo("Done"));
        });
    }

    [Test]
    public async Task DeactivatingAUser_RecordsTheUnassignmentOfTheirItems()
    {
        var item = await CreateAsync("Task");
        await _workItems.AssignAsync(item.Id, _alice.Id);
        var admin = SignInAdmin();

        await UserAdministration().SetActiveAsync(admin.Id, _alice.Id, isActive: false);

        var entry = (await _history.ListForWorkItemAsync(item.Id)).First();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Field, Is.EqualTo(WorkItemField.AssignedToUserId));
            Assert.That(entry.OldValue?.GetGuid(), Is.EqualTo(_alice.Id));
            Assert.That(entry.NewValue, Is.Null);
            Assert.That(entry.AccountId, Is.EqualTo(admin.Id));
        });
    }

    [Test]
    public async Task DeletingAUserWithHistory_IsRefusedAndChangesNothing()
    {
        await CreateAsync("Task");
        var admin = SignInAdmin();

        Assert.ThrowsAsync<UserHasHistoryException>(() => UserAdministration().DeleteAsync(admin.Id, _alice.Id));

        Assert.That(_db.Users.Any(u => u.Id == _alice.Id), Is.True);
    }

    [Test]
    public void List_ForAnItemThatNeverExisted_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _history.ListForWorkItemAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task ListPage_ReturnsTheNewestEntriesUpToTheLimit_AndWhereTheNextPageStarts()
    {
        var item = await RenamedThriceAsync();

        var page = await _history.ListPageForWorkItemAsync(item.Id, offset: 0, limit: 2);

        Assert.Multiple(() =>
        {
            Assert.That(page.Entries.Select(e => e.NewValue?.GetString()), Is.EqualTo(new[] { "Third", "Second" }));
            Assert.That(page.NextOffset, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ListPage_OnTheLastPage_HasNoNextOffset()
    {
        var item = await RenamedThriceAsync();

        var page = await _history.ListPageForWorkItemAsync(item.Id, offset: 4, limit: 2);

        Assert.Multiple(() =>
        {
            Assert.That(page.Entries, Has.Count.EqualTo(1));
            Assert.That(page.NextOffset, Is.Null);
        });
    }

    [Test]
    public async Task ListPage_PastTheEnd_IsEmpty()
    {
        var item = await CreateAsync("Task");

        var page = await _history.ListPageForWorkItemAsync(item.Id, offset: 10, limit: 2);

        Assert.That(page.Entries, Is.Empty);
    }

    [TestCase(-1, 10)]
    [TestCase(0, 0)]
    [TestCase(0, WorkItemHistoryService.MaxPageSize + 1)]
    public async Task ListPage_WithAnInvalidPage_IsRejected(int offset, int limit)
    {
        var item = await CreateAsync("Task");

        Assert.ThrowsAsync<DomainValidationException>(() => _history.ListPageForWorkItemAsync(item.Id, offset, limit));
    }

    [Test]
    public void ListPage_ForAnItemThatNeverExisted_ThrowsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _history.ListPageForWorkItemAsync(Guid.NewGuid(), 0, 10));
    }

    /// <summary>Five entries: the two that creating records, then three renames.</summary>
    private async Task<WorkItem> RenamedThriceAsync()
    {
        var item = await CreateAsync("Zeroth");
        foreach (var title in new[] { "First", "Second", "Third" })
        {
            await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = title });
        }

        return item;
    }

    private const string WeeklyJson =
        "{\"frequency\":\"Weekly\",\"daysOfWeek\":[\"Monday\"],\"startDate\":\"2027-01-04\",\"endDate\":null}";

    private const string MonthlyJson =
        "{\"frequency\":\"Monthly\",\"daysOfWeek\":[],\"startDate\":\"2027-01-04\",\"endDate\":null}";

    /// <summary>Adds an admin and makes it the caller.</summary>
    private User SignInAdmin()
    {
        var admin = TestUsers.Add(_db, "root", isAdmin: true);
        _caller.Current = new Caller(admin.Id, null, null);
        return admin;
    }

    private UserAdministrationService UserAdministration() =>
        new(_db, new AdminAuthorizer(_db), new CountingPasswordHasher(), _clock, _caller);

    private WorkItemAttachmentService AttachmentService() =>
        new(_db, _caller, new InMemoryAttachmentStore(), Options.Create(new AttachmentOptions()));

    private WorkItemRecurrenceService RecurrenceService() => new(
        _db,
        new ServerCalendar(new ServerSettingsService(_db, new AdminAuthorizer(_db)), _clock),
        NullLogger<WorkItemRecurrenceService>.Instance,
        _caller);

    private Task<WorkItem> CreateAsync(string title, Guid? parentId = null) =>
        _workItems.CreateAsync(title, null, parentId, _home.ToDo, spaceId: parentId is null ? _home.Id : null);

    private async Task<WorkItemHistoryView> CreatedEntryAsync(Guid workItemId) =>
        (await _history.ListForWorkItemAsync(workItemId)).Single(e => e.Field == WorkItemHistoryField.Created);

    /// <summary>
    /// The entries after the two that creating the item records ("created" and its Space),
    /// newest first, as (field, old JSON, new JSON).
    /// </summary>
    private async Task<IReadOnlyList<(string Field, string? Old, string? New)>> ChangesAsync(Guid workItemId) =>
        (await _history.ListForWorkItemAsync(workItemId))
        .SkipLast(2)
        .Select(e => (e.Field, e.OldValue?.GetRawText(), e.NewValue?.GetRawText()))
        .ToList();
}
