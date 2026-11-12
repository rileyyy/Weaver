using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Undoing an agent run (ticket 54), after real writes made by an agent that sent a run id.
/// </summary>
[TestFixture]
public class RunUndoServiceTests
{
    private const string RunId = "nightly-1";
    private static readonly DateTimeOffset Night = new(2026, 11, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 11, 16);
    private static readonly DateOnly Friday = new(2026, 11, 20);

    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemLinkService _links = null!;
    private WorkItemUrlLinkService _urlLinks = null!;
    private WorkItemTrashService _trash = null!;
    private CommentService _comments = null!;
    private RunUndoService _undo = null!;
    private User _admin = null!;
    private User _agent = null!;
    private ApiKey _agentKey = null!;
    private TestSpace _home = null!;

    private RunKey Run => new(_agent.Id, _agentKey.Id, RunId);

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(Night);
        _db = TestDatabase.Create(_clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _agent = User.CreateAgent("Runner: llama / When", null);
        _agentKey = ApiKey.Issue(_agent, "nightly", ApiKeyScope.ReadWrite, ApiKeySecret.Hash(ApiKeySecret.Generate()));
        _db.Users.Add(_agent);
        _db.ApiKeys.Add(_agentKey);
        _db.SaveChanges();
        _home = TestSpaces.Add(_db, "Home");
        _caller = new FakeCallerContext(_admin.Id);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        _workItems = new WorkItemService(_db, hierarchy, new FixedServerCalendar(), _caller);
        _links = new WorkItemLinkService(_db, _caller);
        _urlLinks = new WorkItemUrlLinkService(_db, _caller);
        _trash = new WorkItemTrashService(_db, hierarchy, _caller, _clock);
        _comments = new CommentService(_db, _clock, _caller);
        _undo = new RunUndoService(
            _db, new AdminAuthorizer(_db), _caller, _workItems, _links, _urlLinks, _trash);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task DatesTheRunSetOnAnUndatedItem_AreCleared()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Friday);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        var item = await ReadAsync(hall.Id);
        Assert.Multiple(() =>
        {
            Assert.That(item.StartDate, Is.Null);
            Assert.That(item.EndDate, Is.Null);
            Assert.That(result.Changes.Select(c => (c.Field, c.Outcome)), Is.EqualTo(new[]
            {
                (WorkItemField.StartDate, RunUndoOutcome.Undone),
                (WorkItemField.EndDate, RunUndoOutcome.Undone),
            }));
            Assert.That(result.Changes[0].Number, Is.EqualTo(hall.Number));
            Assert.That(result.Changes[0].Title, Is.EqualTo("Paint the hall"));
        });
    }

    [Test]
    public async Task FieldsTheRunChanged_GetTheirValuesFromBeforeTheRun()
    {
        var hall = await CreateAsync("Paint the hall");
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { When = "after drywall" });
        await _workItems.SetTagsAsync(hall.Id, new[] { "paint" });
        AsAgent();
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway", When = "next week" });
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the long hallway" });
        await _workItems.SetTagsAsync(hall.Id, new[] { "paint", "weekend" });
        await _workItems.AssignAsync(hall.Id, _admin.Id);

        await AsAdmin(() => _undo.UndoAsync(Run));

        var item = await ReadAsync(hall.Id);
        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Paint the hall"));
            Assert.That(item.When, Is.EqualTo("after drywall"));
            Assert.That(item.Tags, Is.EqualTo(new[] { "paint" }));
            Assert.That(item.AssignedToUserId, Is.Null);
        });
    }

    [Test]
    public async Task AFieldSomeoneChangedAfterTheRun_IsSkipped_AndTheRestOfTheItemIsUndone()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway", When = "next week" });
        AsAdminNow();
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway blue" });

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        var item = await ReadAsync(hall.Id);
        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Paint the hallway blue"));
            Assert.That(item.When, Is.Null);
            Assert.That(OutcomeOf(result, WorkItemField.Title), Is.EqualTo(RunUndoOutcome.ChangedSince));
            Assert.That(OutcomeOf(result, WorkItemField.When), Is.EqualTo(RunUndoOutcome.Undone));
        });
    }

    [Test]
    public async Task AChangeBetweenTwoOfTheRunsOwn_StillCountsAsChangedSince()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway" });
        AsAdminNow();
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway blue" });
        AsAgent();
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway green" });

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That((await ReadAsync(hall.Id)).Title, Is.EqualTo("Paint the hallway green"));
            Assert.That(result.Changes.Select(c => c.Outcome), Is.All.EqualTo(RunUndoOutcome.ChangedSince));
        });
    }

    [Test]
    public async Task ADateThatWouldStartAfterAnEndSomeoneMoved_IsSkippedAsInvalid()
    {
        var hall = await CreateAsync("Paint the hall");
        await _workItems.RescheduleAsync(hall.Id, Friday, Friday);
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Optional<DateOnly?>.Unset);
        AsAdminNow();
        await _workItems.RescheduleAsync(hall.Id, Optional<DateOnly?>.Unset, Monday);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That((await ReadAsync(hall.Id)).StartDate, Is.EqualTo(Monday));
            Assert.That(OutcomeOf(result, WorkItemField.StartDate), Is.EqualTo(RunUndoOutcome.InvalidDates));
        });
    }

    [Test]
    public async Task LinksTheRunAdded_AreRemovedOnce_AndReportedOnBothItems()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");
        AsAgent();
        await _links.CreateAsync(paint.Id, drywall.Id, WorkItemLinkRelation.BlockedBy);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItemLinks.CountAsync(), Is.Zero);
            Assert.That(result.Changes.Select(c => (c.WorkItemId, c.Outcome)), Is.EquivalentTo(new[]
            {
                (paint.Id, RunUndoOutcome.Undone),
                (drywall.Id, RunUndoOutcome.Undone),
            }));
        });
    }

    [Test]
    public async Task ALinkSomeoneRemovedAndAddedAgain_IsKept()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateAsync("Paint");
        AsAgent();
        var link = await _links.CreateAsync(paint.Id, drywall.Id, WorkItemLinkRelation.BlockedBy);
        AsAdminNow();
        await _links.DeleteAsync(link.Id);
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Related);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItemLinks.CountAsync(), Is.EqualTo(1));
            Assert.That(result.Changes.Select(c => c.Outcome), Is.All.EqualTo(RunUndoOutcome.ChangedSince));
        });
    }

    [Test]
    public async Task UrlLinksTheRunAdded_AreRemoved()
    {
        var hall = await CreateAsync("Paint the hall");
        await _urlLinks.CreateAsync(hall.Id, "https://example.com/colours", "Colours");
        AsAgent();
        await _urlLinks.CreateAsync(hall.Id, "https://example.com/quote", "Quote");

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That(
                (await _urlLinks.ListForWorkItemAsync(hall.Id)).Select(l => l.Url),
                Is.EqualTo(new[] { "https://example.com/colours" }));
            Assert.That(OutcomeOf(result, WorkItemHistoryField.UrlLink), Is.EqualTo(RunUndoOutcome.Undone));
        });
    }

    [Test]
    public async Task CommentsTheRunAdded_AreKept()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _comments.CreateAsync(hall.Id, "Resolved to next week.");

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.Comments.CountAsync(), Is.EqualTo(1));
            Assert.That(OutcomeOf(result, WorkItemHistoryField.Comment), Is.EqualTo(RunUndoOutcome.Kept));
        });
    }

    [Test]
    public async Task SubItemsTheRunCreated_GoToTheTrash_WithWhatSomeoneAddedUnderThem()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        var prep = await _workItems.CreateAsync("Prep the walls", null, hall.Id, _home.ToDo);
        var sand = await _workItems.CreateAsync("Sand", null, prep.Id, _home.ToDo);
        await _comments.CreateAsync(prep.Id, "Added the prep steps.");
        AsAdminNow();
        var fill = await _workItems.CreateAsync("Fill the cracks", null, prep.Id, _home.ToDo);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        var trashed = await _db.WorkItems.IgnoreQueryFilters().Where(w => w.TrashedAtUtc != null).Select(w => w.Id).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(trashed, Is.EquivalentTo(new[] { prep.Id, sand.Id, fill.Id }));
            Assert.That(
                result.Changes.Where(c => c.Field != WorkItemHistoryField.Comment).Select(c => c.Outcome),
                Is.All.EqualTo(RunUndoOutcome.Undone));
            Assert.That(OutcomeOf(result, WorkItemHistoryField.Comment), Is.EqualTo(RunUndoOutcome.Kept));
        });
    }

    [Test]
    public async Task AnItemTheRunDeleted_IsRestored_WithWhatWentWithIt()
    {
        var hall = await CreateAsync("Paint the hall");
        var prep = await _workItems.CreateAsync("Prep the walls", null, hall.Id, _home.ToDo);
        AsAgent();
        await _trash.MoveToTrashAsync(hall.Id, cascade: true);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItems.CountAsync(w => w.Id == hall.Id || w.Id == prep.Id), Is.EqualTo(2));
            Assert.That(result.Changes.Select(c => c.Outcome), Is.All.EqualTo(RunUndoOutcome.Undone));
        });
    }

    [Test]
    public async Task ChangesToAnItemSomeoneDeletedSince_AreReportedAsGone()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Friday);
        AsAdminNow();
        await _trash.MoveToTrashAsync(hall.Id);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.That(result.Changes.Select(c => c.Outcome), Is.All.EqualTo(RunUndoOutcome.ItemGone));
    }

    [Test]
    public async Task AStatusChange_IsNotUndone()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.ChangeStatusAsync(hall.Id, _home.Doing);

        var result = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That((await ReadAsync(hall.Id)).StatusId, Is.EqualTo(_home.Doing));
            Assert.That(OutcomeOf(result, WorkItemField.StatusId), Is.EqualTo(RunUndoOutcome.NotUndoable));
        });
    }

    [Test]
    public async Task TheUndo_IsRecordedInHistory_AsTheAdmins()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Optional<DateOnly?>.Unset);

        await AsAdmin(() => _undo.UndoAsync(Run));

        var last = await _db.WorkItemHistory.OrderBy(e => e.Id).LastAsync();
        Assert.Multiple(() =>
        {
            Assert.That(last.Field, Is.EqualTo(WorkItemField.StartDate));
            Assert.That(last.OldValue, Is.EqualTo("\"2026-11-16\""));
            Assert.That(last.NewValue, Is.Null);
            Assert.That(last.AccountId, Is.EqualTo(_admin.Id));
            Assert.That(last.ApiKeyId, Is.Null);
        });
    }

    [Test]
    public async Task UndoingTwice_ChangesNothingTheSecondTime()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Friday);
        await AsAdmin(() => _undo.UndoAsync(Run));
        var entries = await _db.WorkItemHistory.CountAsync();

        var second = await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.WorkItemHistory.CountAsync(), Is.EqualTo(entries));
            Assert.That(second.Changes.Select(c => c.Outcome), Is.All.EqualTo(RunUndoOutcome.ChangedSince));
        });
    }

    [Test]
    public async Task AnotherAgentsRunWithTheSameId_IsLeftAlone()
    {
        var hall = await CreateAsync("Paint the hall");
        var other = User.CreateAgent("Claude research", null);
        var otherKey = ApiKey.Issue(other, "research", ApiKeyScope.ReadWrite, ApiKeySecret.Hash(ApiKeySecret.Generate()));
        _db.Users.Add(other);
        _db.ApiKeys.Add(otherKey);
        await _db.SaveChangesAsync();
        _caller.Current = new Caller(other.Id, otherKey.Id, null, ApiKeyScope.ReadWrite, RunId);
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { When = "next week" });
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Friday);

        await AsAdmin(() => _undo.UndoAsync(Run));

        Assert.That((await ReadAsync(hall.Id)).When, Is.EqualTo("next week"));
    }

    [Test]
    public void AnUnknownRun_IsNotFound()
    {
        AsAdminNow();

        Assert.ThrowsAsync<RunNotFoundException>(() => _undo.UndoAsync(Run));
    }

    [Test]
    public async Task ANonAdmin_IsRefused_AndNothingIsUndone()
    {
        var hall = await CreateAsync("Paint the hall");
        AsAgent();
        await _workItems.RescheduleAsync(hall.Id, Monday, Friday);
        var member = TestUsers.Add(_db, "member");
        _caller.Current = new Caller(member.Id, null, null);

        Assert.ThrowsAsync<AdminRequiredException>(() => _undo.UndoAsync(Run));
        Assert.That((await ReadAsync(hall.Id)).StartDate, Is.EqualTo(Monday));
    }

    private static RunUndoOutcome OutcomeOf(RunUndoResult result, string field) =>
        result.Changes.Single(c => c.Field == field).Outcome;

    private void AsAgent() =>
        _caller.Current = new Caller(_agent.Id, _agentKey.Id, ActorLabel: null, ApiKeyScope.ReadWrite, RunId);

    private void AsAdminNow() => _caller.Current = new Caller(_admin.Id, null, null);

    private async Task<T> AsAdmin<T>(Func<Task<T>> action)
    {
        AsAdminNow();
        return await action();
    }

    private Task<WorkItem> ReadAsync(Guid id) => _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == id);

    private async Task<WorkItem> CreateAsync(string title)
    {
        AsAdminNow();
        return await _workItems.CreateAsync(title, null, null, _home.ToDo, null, null, null, _home.Id);
    }
}
