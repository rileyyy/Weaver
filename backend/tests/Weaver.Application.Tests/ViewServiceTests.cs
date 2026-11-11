using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class ViewServiceTests
{
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private ViewService _views = null!;
    private WorkItemService _workItems = null!;
    private TestSpace _home = null!;
    private TestSpace _village = null!;
    private User _riley = null!;
    private User _sam = null!;
    private int _lastNumber;

    [SetUp]
    public void SetUp()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(clock);
        _riley = TestUsers.Add(_db, "riley");
        _sam = TestUsers.Add(_db, "sam");
        _caller = new FakeCallerContext(_riley.Id);
        _home = TestSpaces.Add(_db, "Home");
        _village = TestSpaces.Add(_db, "Village");
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        var calendar = new FixedServerCalendar();
        _workItems = new WorkItemService(_db, hierarchy, calendar, _caller);
        var query = new WorkItemQueryService(_db, hierarchy, new InMemoryWorkItemSearch(), calendar);
        _views = new ViewService(_db, _caller, query);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task ListAsync_PutsOverviewFirst_ThenSavedViewsByName()
    {
        await _views.CreateAsync(Definition("Village only", _village.Id));
        await _views.CreateAsync(Definition("Home only", _home.Id));

        var views = await _views.ListAsync();

        Assert.That(views.Select(v => v.Name), Is.EqualTo(new[] { "Overview", "Home only", "Village only" }));
    }

    [Test]
    public async Task CreateAsync_StoresTheView_SharedWithEveryUser()
    {
        var created = await _views.CreateAsync(Definition("Home only", _home.Id) with { Tags = ["outside"] });
        _caller.Current = _caller.Current with { AccountId = _sam.Id };

        var seen = await _views.GetAsync(created.Id);

        Assert.Multiple(() =>
        {
            Assert.That(seen.Name, Is.EqualTo("Home only"));
            Assert.That(seen.SpaceIds, Is.EqualTo(new[] { _home.Id }));
            Assert.That(seen.Tags, Is.EqualTo(new[] { "outside" }));
        });
    }

    [Test]
    public void CreateAsync_WithASpaceThatDoesNotExist_IsNotFound()
    {
        Assert.That(
            () => _views.CreateAsync(Definition("Nowhere", Guid.NewGuid())),
            Throws.TypeOf<EntityNotFoundException>());
    }

    [TestCase("home only")]
    [TestCase("overview")]
    public async Task CreateAsync_WithANameAlreadyTaken_IgnoringCase_IsAConflict(string name)
    {
        await _views.CreateAsync(Definition("Home only", _home.Id));

        Assert.That(() => _views.CreateAsync(Definition(name, _home.Id)), Throws.TypeOf<ViewNameTakenException>());
    }

    [Test]
    public async Task UpdateAsync_RenamesAndRedefinesTheView()
    {
        var view = await _views.CreateAsync(Definition("Home only", _home.Id));

        var updated = await _views.UpdateAsync(view.Id, Definition("Village now", _village.Id) with { Layout = ViewLayout.Roadmap });

        var stored = await _views.GetAsync(view.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated.Name, Is.EqualTo("Village now"));
            Assert.That(stored.SpaceIds, Is.EqualTo(new[] { _village.Id }));
            Assert.That(stored.Layout, Is.EqualTo(ViewLayout.Roadmap));
        });
    }

    [Test]
    public async Task UpdateAsync_KeepingItsOwnName_IsNotAConflict()
    {
        var view = await _views.CreateAsync(Definition("Home only", _home.Id));

        var updated = await _views.UpdateAsync(view.Id, Definition("HOME ONLY", _home.Id));

        Assert.That(updated.Name, Is.EqualTo("HOME ONLY"));
    }

    [Test]
    public async Task UpdateAsync_ToAnotherViewsName_IsAConflict()
    {
        await _views.CreateAsync(Definition("Home only", _home.Id));
        var village = await _views.CreateAsync(Definition("Village only", _village.Id));

        Assert.That(
            () => _views.UpdateAsync(village.Id, Definition("Home only", _village.Id)),
            Throws.TypeOf<ViewNameTakenException>());
    }

    [Test]
    public void UpdateAsync_OnOverview_IsRejected()
    {
        Assert.That(
            () => _views.UpdateAsync(View.OverviewId, Definition("My overview", _home.Id)),
            Throws.TypeOf<BuiltInViewException>());
    }

    [Test]
    public void UpdateAsync_OfAViewThatDoesNotExist_IsNotFound()
    {
        Assert.That(
            () => _views.UpdateAsync(Guid.NewGuid(), Definition("Anything", _home.Id)),
            Throws.TypeOf<EntityNotFoundException>());
    }

    [Test]
    public void DeleteAsync_OfAViewThatDoesNotExist_IsNotFound()
    {
        Assert.That(() => _views.DeleteAsync(Guid.NewGuid()), Throws.TypeOf<EntityNotFoundException>());
    }

    [Test]
    public void DeleteAsync_OnOverview_IsRejected()
    {
        Assert.That(() => _views.DeleteAsync(View.OverviewId), Throws.TypeOf<BuiltInViewException>());
    }

    [Test]
    public async Task DeleteAsync_RemovesTheView()
    {
        var view = await _views.CreateAsync(Definition("Home only", _home.Id));

        await _views.DeleteAsync(view.Id);

        Assert.That(() => _views.GetAsync(view.Id), Throws.TypeOf<EntityNotFoundException>());
    }

    [Test]
    public async Task GetAsync_OfOverview_IsTheBuiltInOne()
    {
        var overview = await _views.GetAsync(View.OverviewId);

        Assert.Multiple(() =>
        {
            Assert.That(overview.IsBuiltIn, Is.True);
            Assert.That(overview.ScheduledOrInProgressOnly, Is.True);
        });
    }

    [Test]
    public async Task GetDefaultAsync_IsOverview_UntilTheUserPicksOne()
    {
        var view = await _views.GetDefaultAsync();

        Assert.That(view.Id, Is.EqualTo(View.OverviewId));
    }

    [Test]
    public async Task SetDefaultAsync_IsPerUser()
    {
        var home = await _views.CreateAsync(Definition("Home only", _home.Id));

        await _views.SetDefaultAsync(home.Id);
        var rileys = await _views.GetDefaultAsync();
        _caller.Current = _caller.Current with { AccountId = _sam.Id };
        var sams = await _views.GetDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rileys.Id, Is.EqualTo(home.Id));
            Assert.That(sams.Id, Is.EqualTo(View.OverviewId));
        });
    }

    [Test]
    public async Task SetDefaultAsync_ToAnotherView_ReplacesTheDefault_AndOverviewClearsIt()
    {
        var home = await _views.CreateAsync(Definition("Home only", _home.Id));
        var village = await _views.CreateAsync(Definition("Village only", _village.Id));

        await _views.SetDefaultAsync(home.Id);
        await _views.SetDefaultAsync(village.Id);
        var afterVillage = await _views.GetDefaultAsync();
        await _views.SetDefaultAsync(View.OverviewId);
        var afterOverview = await _views.GetDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(afterVillage.Id, Is.EqualTo(village.Id));
            Assert.That(afterOverview.Id, Is.EqualTo(View.OverviewId));
            Assert.That(_db.DefaultViews.Count(), Is.Zero);
        });
    }

    [Test]
    public void SetDefaultAsync_ToAViewThatDoesNotExist_IsNotFound()
    {
        Assert.That(() => _views.SetDefaultAsync(Guid.NewGuid()), Throws.TypeOf<EntityNotFoundException>());
    }

    [Test]
    public async Task DeletingTheDefaultView_FallsBackToOverview()
    {
        var home = await _views.CreateAsync(Definition("Home only", _home.Id));
        await _views.SetDefaultAsync(home.Id);

        await _views.DeleteAsync(home.Id);

        Assert.That((await _views.GetDefaultAsync()).Id, Is.EqualTo(View.OverviewId));
    }

    [Test]
    public async Task RunAsync_ListsWhatTheViewsQueryMatches()
    {
        var homeItem = await CreateAsync("Paint the hall", _home, _home.ToDo);
        await CreateAsync("Market stall", _village, _village.ToDo);
        var view = await _views.CreateAsync(Definition("Home only", _home.Id));

        var page = await _views.RunAsync(view.Id, new ViewWindow());

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { homeItem.Id }));
    }

    [Test]
    public async Task RunAsync_OfOverview_ListsDoingAndDatedWorkInEverySpace()
    {
        var doing = await CreateAsync("Started", _home, _home.Doing);
        var dated = await CreateAsync("Market stall", _village, _village.ToDo);
        await _workItems.RescheduleAsync(dated.Id, new DateOnly(2026, 11, 20), null);
        await CreateAsync("Someday", _home, _home.ToDo);

        var page = await _views.RunAsync(View.OverviewId, new ViewWindow());

        Assert.That(page.Items.Select(i => i.Id), Is.EquivalentTo(new[] { doing.Id, dated.Id }));
    }

    [Test]
    public async Task RunAsync_NarrowsByTheWindowGiven()
    {
        var inWindow = await CreateAsync("This week", _home, _home.ToDo);
        await _workItems.RescheduleAsync(inWindow.Id, new DateOnly(2026, 11, 11), null);
        var later = await CreateAsync("Next month", _home, _home.ToDo);
        await _workItems.RescheduleAsync(later.Id, new DateOnly(2026, 12, 11), null);

        var page = await _views.RunAsync(
            View.OverviewId,
            new ViewWindow(From: new DateOnly(2026, 11, 9), To: new DateOnly(2026, 11, 15)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { inWindow.Id }));
    }

    /// <summary>
    /// The query pages by Number, which Postgres generates and InMemory doesn't, so each item
    /// is numbered here.
    /// </summary>
    private async Task<WorkItem> CreateAsync(string title, TestSpace space, Guid statusId)
    {
        var item = await _workItems.CreateAsync(title, null, null, statusId, spaceId: space.Id);
        var tracked = _db.WorkItems.Find(item.Id)!;
        _db.Entry(tracked).Property(w => w.Number).CurrentValue = ++_lastNumber;
        await _db.SaveChangesAsync();
        return item;
    }

    private ViewDefinition Definition(string name, Guid spaceId) => new(
        name,
        [spaceId],
        null,
        [],
        TagMatch.Any,
        ApplyDoneHiding: true,
        ScheduledOrInProgressOnly: false,
        ViewLayout.Board);
}
