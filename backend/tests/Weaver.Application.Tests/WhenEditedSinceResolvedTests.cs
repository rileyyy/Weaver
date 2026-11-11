using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Ticket 49: a human editing the When of an item that already has dates asks for it to be
/// resolved again. Weaver works that out from history, so the query, the summaries and the
/// per-item lookup must agree on it.
/// </summary>
[TestFixture]
public class WhenEditedSinceResolvedTests
{
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemQueryService _query = null!;
    private WorkItemWhenEdits _whenEdits = null!;
    private WorkItemLinkService _links = null!;
    private TestSpace _home = null!;
    private User _human = null!;
    private User _agent = null!;
    private int _lastNumber;

    [SetUp]
    public void SetUp()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(clock);
        _home = TestSpaces.Add(_db, "Home");
        _human = TestUsers.Add(_db, "riley");
        _agent = User.CreateAgent("Runner: llama / When", null);
        _db.Users.Add(_agent);
        _db.SaveChanges();

        _caller = new FakeCallerContext(_human.Id);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        var calendar = new FixedServerCalendar();
        _workItems = new WorkItemService(_db, hierarchy, calendar, _caller);
        _query = new WorkItemQueryService(_db, hierarchy, new InMemoryWorkItemSearch(), calendar);
        _whenEdits = new WorkItemWhenEdits(_db);
        _links = new WorkItemLinkService(_db, _caller);
        _lastNumber = 0;
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task NeverResolved_IsNotEdited_ButStaysUnresolved()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "after drywall"));

        Assert.Multiple(async () =>
        {
            Assert.That(await EditedAsync(item), Is.False);
            Assert.That(await UnresolvedAsync(includeEdited: true), Is.EqualTo(new[] { (item.Id, false) }));
        });
    }

    [Test]
    public async Task ResolvedThenEditedByAHuman_IsEdited()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "next spring"));
        await AsAgent(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsHuman(() => SetWhenAsync(item, "next summer"));

        Assert.Multiple(async () =>
        {
            Assert.That(await EditedAsync(item), Is.True);
            Assert.That(await UnresolvedAsync(includeEdited: true), Is.EqualTo(new[] { (item.Id, true) }));
            Assert.That(await UnresolvedAsync(includeEdited: false), Is.Empty);
        });
    }

    [Test]
    public async Task EditedThenReResolved_IsNotEditedAnyMore()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "next spring"));
        await AsAgent(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsHuman(() => SetWhenAsync(item, "next summer"));
        await AsAgent(() => SetDatesAsync(item, Nov(20), Nov(22)));

        Assert.Multiple(async () =>
        {
            Assert.That(await EditedAsync(item), Is.False);
            Assert.That(await UnresolvedAsync(includeEdited: true), Is.Empty);
        });
    }

    [Test]
    public async Task AHumanChangingTheDatesAfterTheirEdit_ResolvesItThemselves()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "next spring"));
        await AsAgent(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsHuman(() => SetWhenAsync(item, "next summer"));
        await AsHuman(() => SetDatesAsync(item, Nov(20), null));

        Assert.That(await EditedAsync(item), Is.False);
    }

    [Test]
    public async Task AnAgentEditingTheWhen_DoesNotCount()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "next spring"));
        await AsAgent(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsAgent(() => SetWhenAsync(item, "in spring"));

        Assert.Multiple(async () =>
        {
            Assert.That(await EditedAsync(item), Is.False);
            Assert.That(await UnresolvedAsync(includeEdited: true), Is.Empty);
        });
    }

    [Test]
    public async Task AWhenSetByAHumanOnAnItemWhoseDatesCameFirst_IsEdited()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsHuman(() => SetWhenAsync(item, "after drywall"));

        Assert.That(await EditedAsync(item), Is.True);
    }

    // Weaver can't tell which link resolved a When, and agents never remove links, so only
    // dates are re-resolved (ticket 49's decisions).
    [Test]
    public async Task AnItemResolvedByABlockedByLinkOnly_IsNeverEdited()
    {
        var drywall = await CreateAsync("Drywall");
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "after the plaster"));
        await AsAgent(() => _links.CreateAsync(item.Id, drywall.Id, WorkItemLinkRelation.BlockedBy));
        await AsHuman(() => SetWhenAsync(item, "after drywall"));

        Assert.Multiple(async () =>
        {
            Assert.That(await EditedAsync(item), Is.False);
            Assert.That(await UnresolvedAsync(includeEdited: true), Is.Empty);
        });
    }

    [Test]
    public async Task ClearingTheWhen_LeavesNothingToReResolve()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "next spring"));
        await AsAgent(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsHuman(() => SetWhenAsync(item, null));

        Assert.That(await EditedAsync(item), Is.False);
    }

    [Test]
    public async Task SummariesOutsideTheUnresolvedList_AreMarkedToo()
    {
        var item = await CreateAsync("Paint the hall");
        await AsHuman(() => SetWhenAsync(item, "next spring"));
        await AsAgent(() => SetDatesAsync(item, Nov(10), Nov(12)));
        await AsHuman(() => SetWhenAsync(item, "next summer"));

        var page = await _query.QuerySummariesAsync(new WorkItemQuery());

        Assert.That(page.Items.Single().WhenEditedSinceResolved, Is.True);
    }

    [Test]
    public void IncludeEdited_WithoutUnresolvedWhenOnly_IsRejected()
    {
        Assert.That(
            () => _query.QueryAsync(new WorkItemQuery(IncludeEditedWhen: true)),
            Throws.TypeOf<DomainValidationException>());
    }

    private static DateOnly Nov(int day) => new(2026, 11, day);

    private async Task<bool> EditedAsync(WorkItem item) =>
        (await _whenEdits.EditedSinceResolvedAsync([item.Id])).Contains(item.Id);

    private async Task<IReadOnlyList<(Guid, bool)>> UnresolvedAsync(bool includeEdited) =>
        (await _query.QuerySummariesAsync(new WorkItemQuery(UnresolvedWhenOnly: true, IncludeEditedWhen: includeEdited)))
            .Items.Select(s => (s.Id, s.WhenEditedSinceResolved)).ToList();

    private Task AsHuman(Func<Task> write) => As(_human, write);

    private Task AsAgent(Func<Task> write) => As(_agent, write);

    private async Task As(User account, Func<Task> write)
    {
        _caller.Current = new Caller(account.Id, null, null);
        await write();
    }

    private Task SetWhenAsync(WorkItem item, string? when) =>
        _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { When = when });

    private Task SetDatesAsync(WorkItem item, DateOnly? start, DateOnly? end) =>
        _workItems.RescheduleAsync(item.Id, start, end);

    private async Task<WorkItem> CreateAsync(string title)
    {
        var item = await _workItems.CreateAsync(title, null, null, _home.ToDo, spaceId: _home.Id);
        var tracked = _db.WorkItems.Find(item.Id)!;
        _db.Entry(tracked).Property(w => w.Number).CurrentValue = ++_lastNumber;
        _db.SaveChanges();
        return item;
    }
}
