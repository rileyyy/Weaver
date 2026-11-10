using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// The one work-item query (ADR-0006), over InMemory. Text and tag matching and the ancestor walk
/// are stood in for (<see cref="InMemoryWorkItemSearch"/>, <see cref="IterativeWorkItemHierarchy"/>);
/// <c>Weaver.Api.IntegrationTests</c> covers the Postgres versions.
/// </summary>
[TestFixture]
public class WorkItemQueryServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _workItems = null!;
    private WorkItemQueryService _query = null!;
    private TestSpace _home = null!;
    private int _lastNumber;
    private WorkItemTrashService _trash = null!;
    private WorkItemLinkService _links = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _home = TestSpaces.Add(_db, "Home");
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        _workItems = new WorkItemService(_db, hierarchy, FakeCallerContext.Anyone());
        _query = new WorkItemQueryService(_db, hierarchy, new InMemoryWorkItemSearch());
        _trash = new WorkItemTrashService(_db, hierarchy, FakeCallerContext.Anyone(), _clock);
        _links = new WorkItemLinkService(_db, FakeCallerContext.Anyone());
        _lastNumber = 0;
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task NoFilters_ListsEveryItem_InNumberOrder()
    {
        var first = await CreateAsync("Paint the hall");
        var second = await CreateAsync("Fix the gate");

        var page = await _query.QueryAsync(new WorkItemQuery());

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(page.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task Paging_NeitherSkipsNorRepeats_WhenRanksChangeBetweenPages()
    {
        var created = new List<WorkItem>();
        foreach (var title in new[] { "A", "B", "C", "D", "E" })
        {
            created.Add(await CreateAsync(title));
        }

        var first = await _query.QueryAsync(new WorkItemQuery(Limit: 2));
        // Each move puts the item first in its new cell, reshuffling the rank order.
        await _workItems.ChangeStatusAsync(created[4].Id, _home.Doing);
        await _workItems.ChangeStatusAsync(created[0].Id, _home.Doing);
        await _workItems.ChangeStatusAsync(created[4].Id, _home.ToDo);
        var second = await _query.QueryAsync(new WorkItemQuery(Cursor: first.NextCursor, Limit: 2));
        var third = await _query.QueryAsync(new WorkItemQuery(Cursor: second.NextCursor, Limit: 2));

        Assert.Multiple(() =>
        {
            Assert.That(
                first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Title),
                Is.EqualTo(new[] { "A", "B", "C", "D", "E" }));
            Assert.That(first.NextCursor, Is.Not.Null);
            Assert.That(third.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task AnItemCreatedWhilePaging_ComesOnALaterPage()
    {
        await CreateAsync("A");
        await CreateAsync("B");

        var first = await _query.QueryAsync(new WorkItemQuery(Limit: 1));
        await CreateAsync("C");
        var second = await _query.QueryAsync(new WorkItemQuery(Cursor: first.NextCursor, Limit: 5));

        Assert.That(second.Items.Select(i => i.Title), Is.EqualTo(new[] { "B", "C" }));
    }

    [TestCase(0)]
    [TestCase(WorkItemQuery.MaxLimit + 1)]
    public void ALimitOutOfRange_IsRejected(int limit)
    {
        Assert.That(() => _query.QueryAsync(new WorkItemQuery(Limit: limit)), Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public void ACursorThisQueryDidNotIssue_IsRejected()
    {
        var changesCursor = new WorkItemChangesCursor(3).Encode();

        Assert.Multiple(() =>
        {
            Assert.That(() => _query.QueryAsync(new WorkItemQuery(Cursor: "garbage")), Throws.TypeOf<DomainValidationException>());
            Assert.That(() => _query.QueryAsync(new WorkItemQuery(Cursor: changesCursor)), Throws.TypeOf<DomainValidationException>());
        });
    }

    [Test]
    public async Task Spaces_LimitToItemsInAnyOfThem()
    {
        var village = TestSpaces.Add(_db, "Village");
        var software = TestSpaces.Add(_db, "Software");
        var home = await CreateAsync("Hall");
        var fete = await CreateAsync("Fete", space: village);
        await CreateAsync("Bug", space: software);

        var page = await _query.QueryAsync(new WorkItemQuery(SpaceIds: [_home.Id, village.Id]));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { home.Id, fete.Id }));
    }

    [Test]
    public async Task Text_MatchesEveryWord_AsAWordOrItsStart()
    {
        var hall = await CreateAsync("Paint the hallway");
        await CreateAsync("Paint the shed");
        await CreateAsync("Sweep the hall");

        var page = await _query.QueryAsync(new WorkItemQuery(Text: "paint HALL"));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { hall.Id }));
    }

    [Test]
    public async Task Text_MatchesTitleOrDescription_IgnoringCaseAndSurroundingSpace()
    {
        var byTitle = await CreateAsync("Design review");
        var byDescription = await CreateAsync("Kitchen");
        await _workItems.UpdateDetailsAsync(byDescription.Id, new WorkItemDetailsPatch { Description = "needs design" });
        await CreateAsync("Build");

        var page = await _query.QueryAsync(new WorkItemQuery(Text: "  DESIGN "));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { byTitle.Id, byDescription.Id }));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task BlankText_AppliesNoFilter(string text)
    {
        var first = await CreateAsync("Paint the hall");
        var second = await CreateAsync("Fix the gate");

        var page = await _query.QueryAsync(new WorkItemQuery(Text: text));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public async Task Number_FindsThatItem()
    {
        await CreateAsync("One");
        var two = await CreateAsync("Two");

        var page = await _query.QueryAsync(new WorkItemQuery(Number: two.Number));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { two.Id }));
    }

    [TestCase("#2")]
    [TestCase(" #2 ")]
    public async Task TextThatIsAHashNumber_LooksUpThatNumber(string text)
    {
        await CreateAsync("One");
        var two = await CreateAsync("Two");
        await CreateAsync("Item 2 of the list");

        var page = await _query.QueryAsync(new WorkItemQuery(Text: text));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { two.Id }));
    }

    [Test]
    public async Task StatusCategories_LimitToItemsInAnyOfThem()
    {
        var todo = await CreateAsync("Waiting");
        var doing = await CreateAsync("Started");
        var done = await CreateAsync("Finished");
        await _workItems.ChangeStatusAsync(doing.Id, _home.Doing);
        await _workItems.ChangeStatusAsync(done.Id, _home.Done);

        var page = await _query.QueryAsync(new WorkItemQuery(StatusCategories: [StatusCategory.ToDo, StatusCategory.Doing]));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { todo.Id, doing.Id }));
    }

    [Test]
    public async Task Assignee_LimitsToTheirItems()
    {
        var alice = TestUsers.Add(_db, "alice");
        var bob = TestUsers.Add(_db, "bob");
        var hers = await CreateAsync("Hers");
        var his = await CreateAsync("His");
        await CreateAsync("Nobody's");
        await _workItems.AssignAsync(hers.Id, alice.Id);
        await _workItems.AssignAsync(his.Id, bob.Id);

        var page = await _query.QueryAsync(new WorkItemQuery(AssigneeId: alice.Id));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { hers.Id }));
    }

    [Test]
    public async Task Tags_MatchAny_IgnoringCase_ByDefault()
    {
        var urgent = await CreateWithTagsAsync("Urgent one", "Urgent");
        var outside = await CreateWithTagsAsync("Outside one", "outside", "summer");
        await CreateWithTagsAsync("Other", "winter");

        var page = await _query.QueryAsync(new WorkItemQuery(Tags: ["urgent", "OUTSIDE"]));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { urgent.Id, outside.Id }));
    }

    [Test]
    public async Task Tags_WithMatchAll_NeedEveryTag()
    {
        await CreateWithTagsAsync("Just outside", "outside");
        var both = await CreateWithTagsAsync("Both", "Outside", "summer");

        var page = await _query.QueryAsync(new WorkItemQuery(Tags: ["outside", "summer"], TagMatch: TagMatch.All));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { both.Id }));
    }

    [Test]
    public async Task NoTagsOrOnlyBlankOnes_ApplyNoTagFilter()
    {
        var tagged = await CreateWithTagsAsync("Tagged", "outside");
        var untagged = await CreateAsync("Untagged");

        var none = await _query.QueryAsync(new WorkItemQuery(Tags: []));
        var blank = await _query.QueryAsync(new WorkItemQuery(Tags: [" ", ""]));

        Assert.Multiple(() =>
        {
            Assert.That(none.Items.Select(i => i.Id), Is.EqualTo(new[] { tagged.Id, untagged.Id }));
            Assert.That(blank.Items.Select(i => i.Id), Is.EqualTo(new[] { tagged.Id, untagged.Id }));
        });
    }

    [Test]
    public async Task Tags_AreGlobal_AcrossSpaces()
    {
        var village = TestSpaces.Add(_db, "Village");
        var home = await CreateWithTagsAsync("Home", "outside");
        var fete = await CreateWithTagsAsync("Fete", village, "outside");

        var page = await _query.QueryAsync(new WorkItemQuery(Tags: ["outside"]));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { home.Id, fete.Id }));
    }

    [Test]
    public async Task Types_LimitToItemsOfAnyOfThem()
    {
        var project = await CreateAsync("Project");
        var goal = await CreateAsync("Goal");
        var untyped = await CreateAsync("Untyped");
        await _workItems.UpdateDetailsAsync(project.Id, new WorkItemDetailsPatch { TypeId = _home.Project });
        await _workItems.UpdateDetailsAsync(goal.Id, new WorkItemDetailsPatch { TypeId = _home.Goal });

        var page = await _query.QueryAsync(new WorkItemQuery(TypeIds: [_home.Project]));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { project.Id }));
    }

    [Test]
    public async Task UpdatedSince_LimitsToItemsUpdatedAtOrAfterIt()
    {
        await CreateAsync("Old");
        var touched = await CreateAsync("Touched");
        _clock.Advance(TimeSpan.FromDays(1));
        var since = _clock.GetUtcNow();
        await _workItems.UpdateDetailsAsync(touched.Id, new WorkItemDetailsPatch { Title = "Touched again" });

        var page = await _query.QueryAsync(new WorkItemQuery(UpdatedSince: since.ToOffset(TimeSpan.FromHours(10))));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { touched.Id }));
    }

    [Test]
    public async Task Inputs_AndTogether()
    {
        var village = TestSpaces.Add(_db, "Village");
        var match = await CreateWithTagsAsync("Paint the fence", "outside");
        await CreateWithTagsAsync("Paint the hall", "inside");
        await CreateWithTagsAsync("Mend the fence", "outside");
        await CreateWithTagsAsync("Paint the fence", village, "outside");
        var done = await CreateWithTagsAsync("Paint the fence again", "outside");
        await _workItems.ChangeStatusAsync(done.Id, _home.Done);

        var page = await _query.QueryAsync(new WorkItemQuery(
            SpaceIds: [_home.Id],
            Text: "paint",
            Tags: ["outside"],
            StatusCategories: [StatusCategory.ToDo]));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { match.Id }));
    }

    [Test]
    public async Task DateWindow_IncludesItemsWhoseDatesOverlapIt_AndExcludesUndatedOnes()
    {
        var inside = await CreateDatedAsync("Inside", Nov(10), Nov(12));
        var overlapsStart = await CreateDatedAsync("Overlaps start", Nov(1), Nov(9));
        var overlapsEnd = await CreateDatedAsync("Overlaps end", Nov(14), Nov(20));
        var spans = await CreateDatedAsync("Spans", Nov(1), Nov(30));
        await CreateDatedAsync("Before", Nov(1), Nov(8));
        await CreateDatedAsync("After", Nov(15), Nov(20));
        await CreateAsync("Someday");

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(9), To: Nov(14)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { inside.Id, overlapsStart.Id, overlapsEnd.Id, spans.Id }));
    }

    [Test]
    public async Task DateWindow_TreatsAMissingStartOrEndAsOpen_ForAnItemWithTheOtherDate()
    {
        var startedBefore = await CreateDatedAsync("Started, no end", Nov(1), null);
        var endsAfter = await CreateDatedAsync("No start, ends later", null, Nov(30));
        await CreateDatedAsync("Starts after", Nov(20), null);
        await CreateDatedAsync("Ended before", null, Nov(5));

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(9), To: Nov(14)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { startedBefore.Id, endsAfter.Id }));
    }

    [Test]
    public async Task DateWindow_IncludesBothBoundaryDays()
    {
        var startsOnLastDay = await CreateDatedAsync("Starts on the last day", Nov(20), null);
        await CreateDatedAsync("Starts the day after", Nov(21), null);
        var endsOnFirstDay = await CreateDatedAsync("Ends on the first day", null, Nov(10));
        await CreateDatedAsync("Ends the day before", null, Nov(9));

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(10), To: Nov(20)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { startsOnLastDay.Id, endsOnFirstDay.Id }));
    }

    [Test]
    public async Task DateWindow_WithOneBound_IsOpenOnTheOtherSide()
    {
        await CreateDatedAsync("Long ago", new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 2));
        var later = await CreateDatedAsync("Later", Nov(20), Nov(21));
        await CreateAsync("Someday");

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(9)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { later.Id }));
    }

    [Test]
    public async Task DateWindow_WithOnlyAnEnd_IsOpenBeforeIt()
    {
        var longAgo = await CreateDatedAsync("Long ago", new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 2));
        await CreateDatedAsync("Later", Nov(20), Nov(21));
        await CreateAsync("Someday");

        var page = await _query.QueryAsync(new WorkItemQuery(To: Nov(9)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { longAgo.Id }));
    }

    [Test]
    public async Task DateWindow_IncludesAnUndatedAncestor_WhenADescendantMatches()
    {
        var house = await CreateAsync("House");
        var kitchen = await CreateAsync("Kitchen", house.Id);
        var tiles = await CreateDatedAsync("Tiles", Nov(10), Nov(11), kitchen.Id);
        var garden = await CreateAsync("Garden");
        await CreateDatedAsync("Hedge", Nov(20), Nov(21), garden.Id);

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(9), To: Nov(14)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { house.Id, kitchen.Id, tiles.Id }));
    }

    [Test]
    public async Task DateWindow_JudgesADatedParentByItsOwnDates_NotItsChildren()
    {
        var parent = await CreateDatedAsync("Dated parent", Nov(20), Nov(30));
        var child = await CreateDatedAsync("Child", Nov(10), Nov(11), parent.Id);

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(9), To: Nov(14)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { child.Id }));
    }

    [Test]
    public async Task DateWindow_IncludesAnUndatedAncestor_ThroughADatedParentOutsideTheWindow()
    {
        var house = await CreateAsync("House");
        var phase = await CreateDatedAsync("Phase", Nov(20), Nov(30), house.Id);
        var task = await CreateDatedAsync("Task", Nov(10), Nov(11), phase.Id);

        var page = await _query.QueryAsync(new WorkItemQuery(From: Nov(9), To: Nov(14)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { house.Id, task.Id }));
    }

    [Test]
    public async Task DateWindow_IncludesAnUndatedAncestor_OnlyThroughADescendantMatchingTheWholeQuery()
    {
        var tag = "outside";
        var garden = await CreateWithTagsAsync("Garden", tag);
        var hedge = await CreateAsync("Hedge", garden.Id);
        await _workItems.RescheduleAsync(hedge.Id, Nov(10), Nov(11));
        var yard = await CreateWithTagsAsync("Yard", tag);
        var fence = await CreateDatedAsync("Fence", Nov(10), Nov(11), yard.Id);
        await _workItems.SetTagsAsync(fence.Id, new[] { tag });

        var page = await _query.QueryAsync(new WorkItemQuery(Tags: [tag], From: Nov(9), To: Nov(14)));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { yard.Id, fence.Id }));
    }

    [Test]
    public void DateWindow_EndingBeforeItStarts_IsRejected()
    {
        Assert.That(
            () => _query.QueryAsync(new WorkItemQuery(From: Nov(14), To: Nov(9))),
            Throws.TypeOf<DomainValidationException>());
    }

    [Test]
    public async Task BacklogOnly_ListsItemsThatAreNotDoneAndHaveNoDates()
    {
        var todo = await CreateAsync("Someday");
        var doing = await CreateAsync("Started, undated");
        var done = await CreateAsync("Done, undated");
        await CreateDatedAsync("Scheduled", Nov(10), null);
        await CreateDatedAsync("Deadline", null, Nov(10));
        await _workItems.ChangeStatusAsync(doing.Id, _home.Doing);
        await _workItems.ChangeStatusAsync(done.Id, _home.Done);

        var page = await _query.QueryAsync(new WorkItemQuery(BacklogOnly: true));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { todo.Id, doing.Id }));
    }

    [Test]
    public async Task UnresolvedWhenOnly_ListsItemsWithAWhenButNoDatesOrBlocker()
    {
        var unresolved = await CreateWithWhenAsync("Paint the hall", "after drywall");
        await CreateAsync("No When");
        var dated = await CreateWithWhenAsync("Fence", "next spring");
        await _workItems.RescheduleAsync(dated.Id, Nov(10), null);
        var deadline = await CreateWithWhenAsync("Stall", "before the Christmas market");
        await _workItems.RescheduleAsync(deadline.Id, null, Nov(28));

        var page = await _query.QueryAsync(new WorkItemQuery(UnresolvedWhenOnly: true));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { unresolved.Id }));
    }

    [Test]
    public async Task UnresolvedWhenOnly_LeavesOutAnItemABlockedByLinkResolves()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateWithWhenAsync("Paint the hall", "after drywall");
        var related = await CreateWithWhenAsync("Tiles", "after drywall");
        await _links.CreateAsync(paint.Id, drywall.Id, WorkItemLinkRelation.BlockedBy);
        await _links.CreateAsync(related.Id, drywall.Id, WorkItemLinkRelation.Related);

        var page = await _query.QueryAsync(new WorkItemQuery(UnresolvedWhenOnly: true));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { related.Id }));
    }

    // Only a live blocker resolves a When (ADR-0003), so trashing it brings the item back.
    [Test]
    public async Task UnresolvedWhenOnly_ListsAnItemAgain_WhileItsOnlyBlockerIsInTheTrash()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateWithWhenAsync("Paint the hall", "after drywall");
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        await _trash.MoveToTrashAsync(drywall.Id);
        var page = await _query.QueryAsync(new WorkItemQuery(UnresolvedWhenOnly: true));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { paint.Id }));
    }

    [Test]
    public async Task UnresolvedWhenOnly_DoesNotCountABlockerInTheTrash_EvenWithIncludeDeleted()
    {
        var drywall = await CreateAsync("Drywall");
        var paint = await CreateWithWhenAsync("Paint the hall", "after drywall");
        await _links.CreateAsync(drywall.Id, paint.Id, WorkItemLinkRelation.Blocks);

        await _trash.MoveToTrashAsync(drywall.Id);
        var page = await _query.QueryAsync(new WorkItemQuery(UnresolvedWhenOnly: true, IncludeDeleted: true));

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { paint.Id }));
    }

    [Test]
    public async Task UnresolvedWhenOnly_AgreesWithEachItemsOwnRule()
    {
        var drywall = await CreateAsync("Drywall");
        var blocked = await CreateWithWhenAsync("Paint the hall", "after drywall");
        await _links.CreateAsync(drywall.Id, blocked.Id, WorkItemLinkRelation.Blocks);
        var dated = await CreateWithWhenAsync("Fence", "next spring");
        await _workItems.RescheduleAsync(dated.Id, Nov(10), Nov(12));
        await CreateWithWhenAsync("Stall", "before the Christmas market");
        await CreateAsync("No When");
        var blockedIds = new HashSet<Guid> { blocked.Id };

        var all = (await _query.QueryAsync(new WorkItemQuery())).Items;
        var unresolved = (await _query.QueryAsync(new WorkItemQuery(UnresolvedWhenOnly: true))).Items;

        Assert.That(
            unresolved.Select(i => i.Id),
            Is.EqualTo(all.Where(i => i.When is not null && !i.IsWhenResolved(blockedIds.Contains(i.Id))).Select(i => i.Id)));
    }

    [Test]
    public async Task Summaries_AreTheSamePage_InBrief_WithNames()
    {
        var alice = TestUsers.Add(_db, "alice");
        var parent = await CreateAsync("Kitchen");
        await CreateAsync("Tiles", parent.Id);
        await CreateAsync("Garden");
        await _workItems.ChangeStatusAsync(parent.Id, _home.Doing);
        await _workItems.AssignAsync(parent.Id, alice.Id);
        await _workItems.UpdateDetailsAsync(parent.Id, new WorkItemDetailsPatch { TypeId = _home.Project });

        var page = await _query.QuerySummariesAsync(new WorkItemQuery(Limit: 1));

        var summary = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(summary.Id, Is.EqualTo(parent.Id));
            Assert.That(summary.Title, Is.EqualTo("Kitchen"));
            Assert.That(summary.SpaceName, Is.EqualTo("Home"));
            Assert.That(summary.StatusName, Is.EqualTo("Doing"));
            Assert.That(summary.TypeName, Is.EqualTo("Project"));
            Assert.That(summary.AssigneeName, Is.EqualTo("alice"));
            Assert.That(summary.ChildCount, Is.EqualTo(1));
            Assert.That(page.NextCursor, Is.Not.Null);
        });
    }

    [Test]
    public async Task TrashedItems_AreLeftOut()
    {
        var kept = await CreateAsync("Kept");
        var trashed = await CreateAsync("Trashed");
        await _trash.MoveToTrashAsync(trashed.Id);

        var page = await _query.QueryAsync(new WorkItemQuery());

        Assert.That(page.Items.Select(w => w.Id), Is.EqualTo(new[] { kept.Id }));
    }

    [Test]
    public async Task IncludeDeleted_ListsTrashedItemsToo_MarkedAsTrashed()
    {
        var kept = await CreateAsync("Kept");
        var trashed = await CreateAsync("Trashed");
        await _trash.MoveToTrashAsync(trashed.Id);

        var items = (await _query.QueryAsync(new WorkItemQuery(IncludeDeleted: true))).Items;
        var summaries = (await _query.QuerySummariesAsync(new WorkItemQuery(IncludeDeleted: true))).Items;

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(w => (w.Id, w.IsTrashed)), Is.EqualTo(new[] { (kept.Id, false), (trashed.Id, true) }));
            Assert.That(summaries.Select(s => s.TrashedAtUtc is not null), Is.EqualTo(new[] { false, true }));
        });
    }

    [Test]
    public async Task IncludeDeleted_StillAppliesTheOtherFilters()
    {
        await CreateAsync("Paint the hall");
        var trashed = await CreateAsync("Paint the gate");
        await CreateAsync("Fix the boiler");
        await _trash.MoveToTrashAsync(trashed.Id);

        var page = await _query.QueryAsync(new WorkItemQuery(Text: "gate", IncludeDeleted: true));

        Assert.That(page.Items.Select(w => w.Id), Is.EqualTo(new[] { trashed.Id }));
    }

    [Test]
    public async Task ChildCount_LeavesTrashedChildrenOut()
    {
        var parent = await CreateAsync("Parent");
        var child = await CreateAsync("Child", parent.Id);
        await CreateAsync("Sibling", parent.Id);
        await _trash.MoveToTrashAsync(child.Id);

        var page = await _query.QuerySummariesAsync(new WorkItemQuery(Number: parent.Number));

        Assert.That(page.Items.Single().ChildCount, Is.EqualTo(1));
    }

    // IgnoreQueryFilters lifts the Trash filter from navigations too, so the count says so itself.
    [Test]
    public async Task ChildCount_LeavesTrashedChildrenOut_EvenWithIncludeDeleted()
    {
        var parent = await CreateAsync("Parent");
        var child = await CreateAsync("Child", parent.Id);
        await CreateAsync("Sibling", parent.Id);
        await _trash.MoveToTrashAsync(child.Id);

        var page = await _query.QuerySummariesAsync(new WorkItemQuery(Number: parent.Number, IncludeDeleted: true));

        Assert.That(page.Items.Single().ChildCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Summaries_CarryEachItemsParent_SoAFlatListRebuildsTheTree()
    {
        var parent = await CreateAsync("Parent");
        var child = await CreateAsync("Child", parent.Id);

        var page = await _query.QuerySummariesAsync(new WorkItemQuery());

        Assert.That(
            page.Items.Select(s => (s.Id, s.ParentId)),
            Is.EqualTo(new (Guid, Guid?)[] { (parent.Id, null), (child.Id, parent.Id) }));
    }

    private static DateOnly Nov(int day) => new(2026, 11, day);

    private async Task<WorkItem> CreateDatedAsync(string title, DateOnly? start, DateOnly? end, Guid? parentId = null)
    {
        var item = await CreateAsync(title, parentId);
        await _workItems.RescheduleAsync(item.Id, start, end);
        return item;
    }

    private async Task<WorkItem> CreateWithWhenAsync(string title, string when)
    {
        var item = await CreateAsync(title);
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { When = when });
        return item;
    }

    private Task<WorkItem> CreateWithTagsAsync(string title, params string[] tags) => CreateWithTagsAsync(title, _home, tags);

    private async Task<WorkItem> CreateWithTagsAsync(string title, TestSpace space, params string[] tags)
    {
        var item = await CreateAsync(title, space: space);
        await _workItems.SetTagsAsync(item.Id, tags);
        return item;
    }

    private async Task<WorkItem> CreateAsync(string title, Guid? parentId = null, TestSpace? space = null)
    {
        var item = await _workItems.CreateAsync(
            title,
            null,
            parentId,
            (space ?? _home).ToDo,
            spaceId: parentId is null ? (space ?? _home).Id : null);
        NumberLikePostgres(item);
        return item;
    }

    /// <summary>
    /// Postgres numbers items from an identity column, which InMemory doesn't generate, and the
    /// query orders and pages by Number.
    /// </summary>
    private void NumberLikePostgres(WorkItem item)
    {
        var tracked = _db.WorkItems.Find(item.Id)!;
        _db.Entry(tracked).Property(w => w.Number).CurrentValue = ++_lastNumber;
        _db.SaveChanges();
    }
}
