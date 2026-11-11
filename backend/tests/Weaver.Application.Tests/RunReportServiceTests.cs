using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Run reports (ticket 50), read through <see cref="IRunReportService"/> after real writes made
/// by agents that sent a run id.
/// </summary>
[TestFixture]
public class RunReportServiceTests
{
    private static readonly DateTimeOffset Night = new(2026, 11, 10, 2, 0, 0, TimeSpan.Zero);

    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private RunReportService _runs = null!;
    private User _admin = null!;
    private User _when = null!;
    private ApiKey _whenKey = null!;
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(Night);
        _db = TestDatabase.Create(_clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        (_when, _whenKey) = AddAgent("Runner: llama / When", "nightly");
        _home = TestSpaces.Add(_db, "Home");
        _caller = new FakeCallerContext(_admin.Id);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), _caller);
        _trash = new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), _caller, _clock);
        _runs = new RunReportService(_db, new AdminAuthorizer(_db), _caller);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task OneRunsWrites_AreListedAsOneRun_WithItsAgentKeyTimesAndCount()
    {
        var hall = await CreateAsync("Paint the hall");
        var shed = await CreateAsync("Fix the shed");
        ActAs(_when, _whenKey, "run-1");
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway" });
        _clock.Advance(TimeSpan.FromMinutes(5));
        await _workItems.ChangeStatusAsync(shed.Id, _home.Doing);

        var page = await AsAdmin(() => _runs.ListAsync(0, 20));

        Assert.Multiple(() =>
        {
            Assert.That(page.Runs, Has.Count.EqualTo(1));
            var run = page.Runs[0];
            Assert.That(run.RunId, Is.EqualTo("run-1"));
            Assert.That(run.AccountId, Is.EqualTo(_when.Id));
            Assert.That(run.AccountName, Is.EqualTo("Runner: llama / When"));
            Assert.That(run.ApiKeyId, Is.EqualTo(_whenKey.Id));
            Assert.That(run.ApiKeyLabel, Is.EqualTo("nightly"));
            Assert.That(run.FirstChangeAtUtc, Is.EqualTo(Night));
            Assert.That(run.LastChangeAtUtc, Is.EqualTo(Night.AddMinutes(5)));
            Assert.That(run.ChangeCount, Is.EqualTo(2));
            Assert.That(page.NextOffset, Is.Null);
        });
    }

    [Test]
    public async Task WritesWithoutARunId_AreNotARun()
    {
        var item = await CreateAsync("Paint the hall");
        ActAs(_when, _whenKey, runId: null);
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Paint the hallway" });

        var page = await AsAdmin(() => _runs.ListAsync(0, 20));

        Assert.That(page.Runs, Is.Empty);
    }

    [Test]
    public async Task AHumansWritesWithARunId_AreNotARun()
    {
        var item = await CreateAsync("Paint the hall");
        _caller.Current = new Caller(_admin.Id, null, null, RunId: "by-hand");
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Paint the hallway" });

        var page = await AsAdmin(() => _runs.ListAsync(0, 20));

        Assert.That(page.Runs, Is.Empty);
    }

    [Test]
    public async Task TheSameRunIdFromTwoAgents_IsTwoRuns()
    {
        var item = await CreateAsync("Paint the hall");
        var (research, researchKey) = AddAgent("Claude: research", "hosted");
        ActAs(_when, _whenKey, "shared-id");
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "One" });
        ActAs(research, researchKey, "shared-id");
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Two" });

        var page = await AsAdmin(() => _runs.ListAsync(0, 20));

        Assert.That(page.Runs.Select(r => (r.AccountName, r.RunId)), Is.EquivalentTo(new[]
        {
            ("Runner: llama / When", "shared-id"),
            ("Claude: research", "shared-id"),
        }));
    }

    [Test]
    public async Task Runs_AreNewestFirst_ByTheirLastChange_AndPaged()
    {
        var item = await CreateAsync("Paint the hall");
        foreach (var runId in new[] { "monday", "tuesday", "wednesday" })
        {
            ActAs(_when, _whenKey, runId);
            await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = runId });
            _clock.Advance(TimeSpan.FromDays(1));
        }

        var first = await AsAdmin(() => _runs.ListAsync(0, 2));
        var second = await AsAdmin(() => _runs.ListAsync(first.NextOffset!.Value, 2));

        Assert.Multiple(() =>
        {
            Assert.That(first.Runs.Select(r => r.RunId), Is.EqualTo(new[] { "wednesday", "tuesday" }));
            Assert.That(first.NextOffset, Is.EqualTo(2));
            Assert.That(second.Runs.Select(r => r.RunId), Is.EqualTo(new[] { "monday" }));
            Assert.That(second.NextOffset, Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(RunReportService.MaxPageSize + 1)]
    public void ListAsync_WithALimitOutOfRange_IsRejected(int limit)
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _runs.ListAsync(0, limit));
    }

    [Test]
    public void ListAsync_WithANegativeOffset_IsRejected()
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _runs.ListAsync(-1, 20));
    }

    [Test]
    public void ListAsync_ForANonAdmin_IsRefused()
    {
        _caller.Current = new Caller(TestUsers.Add(_db, "bob").Id, null, null);

        Assert.ThrowsAsync<AdminRequiredException>(() => _runs.ListAsync(0, 20));
    }

    [Test]
    public void ListAsync_ForAnAgent_IsRefused()
    {
        ActAs(_when, _whenKey, runId: null);

        Assert.ThrowsAsync<AgentAdministrationException>(() => _runs.ListAsync(0, 20));
    }

    [Test]
    public async Task ARunsChanges_AreItsItems_InTheOrderItChangedThem_WithOnlyThatRunsEntries()
    {
        var hall = await CreateAsync("Paint the hall");
        var shed = await CreateAsync("Fix the shed");
        ActAs(_when, _whenKey, "run-1");
        await _workItems.ChangeStatusAsync(shed.Id, _home.Doing);
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { When = "next week" });
        ActAs(_when, _whenKey, "run-2");
        await _workItems.UpdateDetailsAsync(hall.Id, new WorkItemDetailsPatch { Title = "Paint the hallway" });

        var items = await AsAdmin(() => _runs.ListChangesAsync(new RunKey(_when.Id, _whenKey.Id, "run-1")));

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(i => i.Item.Title), Is.EqualTo(new[] { "Fix the shed", "Paint the hallway" }));
            Assert.That(items.Select(i => i.Item.Number), Is.EqualTo(new int?[] { shed.Number, hall.Number }));
            Assert.That(items[0].Changes.Select(c => (c.Field, c.NewName)), Is.EqualTo(new[] { (WorkItemField.StatusId, (string?)"Doing") }));
            Assert.That(items[1].Changes.Select(c => c.Field), Is.EqualTo(new[] { WorkItemField.When }));
            Assert.That(items.SelectMany(i => i.Changes).Select(c => c.RunId), Is.All.EqualTo("run-1"));
        });
    }

    [Test]
    public async Task AnItemTheRunDeleted_IsMarkedDeleted()
    {
        var item = await CreateAsync("Old shed");
        ActAs(_when, _whenKey, "run-1");
        await _trash.MoveToTrashAsync(item.Id);

        var changed = (await AsAdmin(() => _runs.ListChangesAsync(new RunKey(_when.Id, _whenKey.Id, "run-1")))).Single();

        Assert.Multiple(() =>
        {
            Assert.That(changed.Deleted, Is.True);
            Assert.That(changed.Item.Title, Is.EqualTo("Old shed"));
        });
    }

    [Test]
    public async Task ARunsChanges_ForARunThatDoesNotExist_IsNotFound()
    {
        var item = await CreateAsync("Paint the hall");
        ActAs(_when, _whenKey, "run-1");
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Renamed" });

        Assert.ThrowsAsync<RunNotFoundException>(() =>
            AsAdmin(() => _runs.ListChangesAsync(new RunKey(_when.Id, _whenKey.Id, "run-2"))));
    }

    [Test]
    public async Task ARunsChanges_ForARunThatDoesNotExist_NamesTheWholeRunInTheError()
    {
        var item = await CreateAsync("Paint the hall");
        ActAs(_when, _whenKey, "run-1");
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Renamed" });
        var otherKey = Guid.NewGuid();

        var error = Assert.ThrowsAsync<RunNotFoundException>(() =>
            AsAdmin(() => _runs.ListChangesAsync(new RunKey(_when.Id, otherKey, "run-1"))));

        Assert.That(error!.Message, Does.Contain("run-1").And.Contain(_when.Id.ToString()).And.Contain(otherKey.ToString()));
    }

    [Test]
    public void ARunsChanges_ForANonAdmin_IsRefused()
    {
        _caller.Current = new Caller(TestUsers.Add(_db, "bob").Id, null, null);

        Assert.ThrowsAsync<AdminRequiredException>(() =>
            _runs.ListChangesAsync(new RunKey(_when.Id, _whenKey.Id, "run-1")));
    }

    private (User Agent, ApiKey Key) AddAgent(string name, string keyLabel)
    {
        var agent = User.CreateAgent(name, null);
        var key = ApiKey.Issue(agent, keyLabel, ApiKeyScope.ReadWrite, ApiKeySecret.Hash(ApiKeySecret.Generate()));
        _db.Users.Add(agent);
        _db.ApiKeys.Add(key);
        _db.SaveChanges();
        return (agent, key);
    }

    private void ActAs(User agent, ApiKey key, string? runId) =>
        _caller.Current = new Caller(agent.Id, key.Id, ActorLabel: null, ApiKeyScope.ReadWrite, runId);

    private async Task<T> AsAdmin<T>(Func<Task<T>> read)
    {
        _caller.Current = new Caller(_admin.Id, null, null);
        return await read();
    }

    private async Task<WorkItem> CreateAsync(string title)
    {
        _caller.Current = new Caller(_admin.Id, null, null);
        return await _workItems.CreateAsync(title, null, null, _home.ToDo, null, null, null, _home.Id);
    }
}
