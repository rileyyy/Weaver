using static Weaver.Application.Tests.Patches;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemService _service = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _service = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), FakeCallerContext.Anyone());
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public void CreateAsync_WithUnknownParent_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Task", null, parentId: Guid.NewGuid(), _space.ToDo));
    }

    [Test]
    public void CreateAsync_WithUnknownStatus_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Task", null, parentId: null, statusId: Guid.NewGuid(), spaceId: _space.Id));
    }

    [Test]
    public async Task ChangeStatusAsync_MovesColumn_ButNeverChangesParent()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var child = await _service.CreateAsync("Child", null, parent.Id, _space.ToDo);

        var moved = (await _service.ChangeStatusAsync(child.Id, _space.Doing)).Item;

        Assert.That(moved.StatusId, Is.EqualTo(_space.Doing));
        Assert.That(moved.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task ReparentAsync_ChangesParent_ButNeverChangesStatus()
    {
        var oldParent = await _service.CreateAsync("Old parent", null, null, _space.ToDo, spaceId: _space.Id);
        var newParent = await _service.CreateAsync("New parent", null, null, _space.ToDo, spaceId: _space.Id);
        var child = await _service.CreateAsync("Child", null, oldParent.Id, _space.Doing);

        var moved = (await _service.ReparentAsync(child.Id, newParent.Id)).Item;

        Assert.That(moved.ParentId, Is.EqualTo(newParent.Id));
        Assert.That(moved.StatusId, Is.EqualTo(_space.Doing));
    }

    [Test]
    public async Task ReparentAsync_ToItself_ThrowsCyclicParentException()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<CyclicParentException>(() => _service.ReparentAsync(item.Id, item.Id));
    }

    [Test]
    public async Task ReparentAsync_ToOwnDescendant_ThrowsCyclicParentException()
    {
        var root = await _service.CreateAsync("Root", null, null, _space.ToDo, spaceId: _space.Id);
        var child = await _service.CreateAsync("Child", null, root.Id, _space.ToDo);
        var grandchild = await _service.CreateAsync("Grandchild", null, child.Id, _space.ToDo);

        Assert.ThrowsAsync<CyclicParentException>(() => _service.ReparentAsync(root.Id, grandchild.Id));
    }

    [Test]
    public async Task ReparentAsync_ToUnrelatedItem_Succeeds()
    {
        var root = await _service.CreateAsync("Root", null, null, _space.ToDo, spaceId: _space.Id);
        var child = await _service.CreateAsync("Child", null, root.Id, _space.ToDo);
        var unrelated = await _service.CreateAsync("Unrelated", null, null, _space.ToDo, spaceId: _space.Id);

        var moved = (await _service.ReparentAsync(child.Id, unrelated.Id)).Item;

        Assert.That(moved.ParentId, Is.EqualTo(unrelated.Id));
    }

    [Test]
    public async Task CreateAsync_AfterASiblingThatSharesItsRank_RespacesTheCell()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var first = await _service.CreateAsync("First", null, parent.Id, _space.ToDo);
        var second = await _service.CreateAsync("Second", null, parent.Id, _space.ToDo, afterId: first.Id);
        second.Reposition(first.Rank);
        await _db.SaveChangesAsync();

        var inserted = await _service.CreateAsync("Inserted", null, parent.Id, _space.ToDo, afterId: first.Id);

        // Which of the two tied items comes first is decided by Number, which InMemory
        // doesn't generate; what must hold is distinct ranks with the new item right after
        // its anchor.
        var cell = (await _service.GetChildrenAsync(parent.Id)).Select(w => w.Id).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(new[] { first.Rank, second.Rank, inserted.Rank }, Is.Unique);
            Assert.That(cell.IndexOf(inserted.Id), Is.EqualTo(cell.IndexOf(first.Id) + 1));
        });
    }

    [Test]
    public async Task Timestamps_ComeFromTheClock_AndOnlyUpdatedAtMovesOnChange()
    {
        var created = _clock.GetUtcNow();
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);
        _clock.Advance(TimeSpan.FromHours(1));

        await _service.RescheduleAsync(item.Id, new DateOnly(2026, 9, 2), null);

        Assert.Multiple(() =>
        {
            Assert.That(item.CreatedAtUtc, Is.EqualTo(created));
            Assert.That(item.UpdatedAtUtc, Is.EqualTo(created + TimeSpan.FromHours(1)));
        });
    }

    [Test]
    public async Task GetAllAsync_PagesInRankThenNumberOrder()
    {
        var first = await _service.CreateAsync("First", null, null, _space.ToDo, spaceId: _space.Id);
        var second = await _service.CreateAsync("Second", null, null, _space.ToDo, afterId: first.Id, spaceId: _space.Id);
        var third = await _service.CreateAsync("Third", null, null, _space.ToDo, afterId: second.Id, spaceId: _space.Id);

        var page1 = await _service.GetAllAsync(offset: 0, limit: 2);
        var page2 = await _service.GetAllAsync(offset: 2, limit: 2);

        Assert.Multiple(() =>
        {
            Assert.That(page1.Select(w => w.Id), Is.EqualTo(new[] { first.Id, second.Id }));
            Assert.That(page2.Select(w => w.Id), Is.EqualTo(new[] { third.Id }));
        });
    }

    [TestCase(-1, 10)]
    [TestCase(0, 0)]
    [TestCase(0, WorkItemService.MaxPageSize + 1)]
    public void GetAllAsync_WithAnInvalidPage_IsRejected(int offset, int limit)
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _service.GetAllAsync(offset, limit));
    }

    [Test]
    public async Task ReparentAsync_WhenTheTreeAlreadyHasACycle_ThrowsInsteadOfReportingNoCycle()
    {
        var a = await _service.CreateAsync("A", null, null, _space.ToDo, spaceId: _space.Id);
        var b = await _service.CreateAsync("B", null, a.Id, _space.ToDo);
        var outsider = await _service.CreateAsync("Outsider", null, null, _space.ToDo, spaceId: _space.Id);
        _db.Entry(a).Property(w => w.ParentId).CurrentValue = b.Id;
        await _db.SaveChangesAsync();

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.ReparentAsync(outsider.Id, a.Id));
    }

    [Test]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetChildrenAsync_ReturnsOnlyDirectChildren_OrderedByRank()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var first = await _service.CreateAsync("First", null, parent.Id, _space.ToDo);
        var second = await _service.CreateAsync("Second", null, parent.Id, _space.ToDo, afterId: first.Id);
        await _service.CreateAsync("Grandchild", null, second.Id, _space.ToDo);

        var children = await _service.GetChildrenAsync(parent.Id);

        Assert.That(children.Select(c => c.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public async Task GetAllAsync_ReturnsEveryItem_RegardlessOfParent()
    {
        var root = await _service.CreateAsync("Root", null, null, _space.ToDo, spaceId: _space.Id);
        var child = await _service.CreateAsync("Child", null, root.Id, _space.ToDo);
        var grandchild = await _service.CreateAsync("Grandchild", null, child.Id, _space.ToDo);
        var unrelated = await _service.CreateAsync("Unrelated", null, null, _space.ToDo, spaceId: _space.Id);

        var all = await _service.GetAllAsync(offset: 0, limit: 100);

        Assert.That(
            all.Select(w => w.Id),
            Is.EquivalentTo(new[] { root.Id, child.Id, grandchild.Id, unrelated.Id }));
    }

    [Test]
    public async Task RescheduleAsync_SetsStartAndEndDate_WithoutTouchingStatusOrParent()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var item = await _service.CreateAsync("Item", null, parent.Id, _space.ToDo);
        var start = new DateOnly(2026, 9, 25);
        var end = start.AddDays(7);

        var rescheduled = (await _service.RescheduleAsync(item.Id, start, end)).Item;

        Assert.That(rescheduled.StartDate, Is.EqualTo(start));
        Assert.That(rescheduled.EndDate, Is.EqualTo(end));
        Assert.That(rescheduled.StatusId, Is.EqualTo(_space.ToDo));
        Assert.That(rescheduled.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task RescheduleAsync_WithNullDates_ClearsAnExistingSchedule()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);
        await _service.RescheduleAsync(item.Id, new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 26));

        var cleared = (await _service.RescheduleAsync(item.Id, null, null)).Item;

        Assert.That(cleared.StartDate, Is.Null);
        Assert.That(cleared.EndDate, Is.Null);
    }

    [Test]
    public async Task RescheduleAsync_WithStartAfterEnd_ThrowsInvalidWorkItemScheduleException()
    {
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);
        var start = new DateOnly(2026, 9, 25);
        var end = start.AddDays(-1);

        Assert.ThrowsAsync<InvalidWorkItemScheduleException>(() =>
            _service.RescheduleAsync(item.Id, start, end));
    }

    [Test]
    public void RescheduleAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.RescheduleAsync(Guid.NewGuid(), null, null));
    }

    [Test]
    public async Task ChangeStatusAsync_WithAfterId_OrdersCardBetweenNeighborsInDestinationCell()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var first = await _service.CreateAsync("First", null, parent.Id, _space.Doing);
        var second = await _service.CreateAsync("Second", null, parent.Id, _space.Doing, afterId: first.Id);
        var moving = await _service.CreateAsync("Moving", null, parent.Id, _space.ToDo);

        var moved = (await _service.ChangeStatusAsync(moving.Id, _space.Doing, afterId: first.Id)).Item;

        Assert.That(moved.Rank, Is.GreaterThan(first.Rank));
        Assert.That(moved.Rank, Is.LessThan(second.Rank));
    }

    [Test]
    public async Task ChangeStatusAsync_ToItsOwnStatus_ReordersWithinTheCell()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var first = await _service.CreateAsync("First", null, parent.Id, _space.ToDo);
        var second = await _service.CreateAsync("Second", null, parent.Id, _space.ToDo, afterId: first.Id);
        var third = await _service.CreateAsync("Third", null, parent.Id, _space.ToDo, afterId: second.Id);

        await _service.ChangeStatusAsync(third.Id, _space.ToDo, afterId: first.Id);

        var cell = (await _service.GetChildrenAsync(parent.Id)).Select(w => w.Id);
        Assert.That(cell, Is.EqualTo(new[] { first.Id, third.Id, second.Id }));
    }

    [Test]
    public async Task ChangeStatusAsync_ToItsOwnStatusWithoutAnchor_MovesToTheTopOfTheCell()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var first = await _service.CreateAsync("First", null, parent.Id, _space.ToDo);
        var second = await _service.CreateAsync("Second", null, parent.Id, _space.ToDo, afterId: first.Id);

        await _service.ChangeStatusAsync(second.Id, _space.ToDo, afterId: null);

        var cell = (await _service.GetChildrenAsync(parent.Id)).Select(w => w.Id);
        Assert.That(cell, Is.EqualTo(new[] { second.Id, first.Id }));
    }

    [Test]
    public async Task ChangeStatusAsync_AfterASiblingThatSharesItsRank_RespacesTheCell()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var first = await _service.CreateAsync("First", null, parent.Id, _space.ToDo);
        var second = await _service.CreateAsync("Second", null, parent.Id, _space.ToDo, afterId: first.Id);
        var moving = await _service.CreateAsync("Moving", null, parent.Id, _space.ToDo, afterId: second.Id);
        second.Reposition(first.Rank);
        await _db.SaveChangesAsync();

        await _service.ChangeStatusAsync(moving.Id, _space.ToDo, afterId: first.Id);

        var cell = (await _service.GetChildrenAsync(parent.Id)).Select(w => w.Id).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(new[] { first.Rank, second.Rank, moving.Rank }, Is.Unique);
            Assert.That(cell.IndexOf(moving.Id), Is.EqualTo(cell.IndexOf(first.Id) + 1));
        });
    }

    [Test]
    public async Task CreateAsync_WithTypeId_SetsTheType()
    {
        var item = await _service.CreateAsync(
            "Task", null, null, _space.ToDo, typeId: _space.Task, spaceId: _space.Id);

        Assert.That(item.TypeId, Is.EqualTo(_space.Task));
    }

    [Test]
    public void CreateAsync_WithUnknownTypeId_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Task", null, null, _space.ToDo, typeId: Guid.NewGuid(), spaceId: _space.Id));
    }

    [Test]
    public async Task CreateAsync_LeavesPriorityUnsetByDefault()
    {
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.That(item.PriorityId, Is.Null);
    }

    [Test]
    public async Task CreateAsync_WithPriorityId_SetsIt()
    {
        var item = await _service.CreateAsync(
            "Task", null, null, _space.ToDo, priorityId: _space.Urgent, spaceId: _space.Id);

        Assert.That(item.PriorityId, Is.EqualTo(_space.Urgent));
    }

    [Test]
    public async Task CreateAsync_WithUnknownPriorityId_ThrowsAndCreatesNothing()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Task", null, null, _space.ToDo, priorityId: Guid.NewGuid(), spaceId: _space.Id));

        Assert.That(await _db.WorkItems.AnyAsync(), Is.False);
    }

    [Test]
    public async Task UpdateDetailsAsync_UpdatesTitleDescriptionTypeAndPriority()
    {
        var item = await _service.CreateAsync("Old title", "Old description", null, _space.ToDo, spaceId: _space.Id);

        var updated = (await _service.UpdateDetailsAsync(
            item.Id, Details("New title", "New description", _space.Goal, _space.Urgent))).Item;

        Assert.That(updated.Title, Is.EqualTo("New title"));
        Assert.That(updated.Description, Is.EqualTo("New description"));
        Assert.That(updated.TypeId, Is.EqualTo(_space.Goal));
        Assert.That(updated.PriorityId, Is.EqualTo(_space.Urgent));
    }

    [Test]
    public async Task UpdateDetailsAsync_NeverChangesStatusOrParent()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var item = await _service.CreateAsync("Child", null, parent.Id, _space.Doing);

        var updated = (await _service.UpdateDetailsAsync(item.Id, Details("Renamed", null, null, _space.Low))).Item;

        Assert.That(updated.StatusId, Is.EqualTo(_space.Doing));
        Assert.That(updated.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public void UpdateDetailsAsync_WithUnknownTypeId_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.UpdateDetailsAsync(Guid.NewGuid(), Details("Title", null, Guid.NewGuid(), null)));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithUnknownPriorityId_ThrowsAndLeavesPriorityUnchanged()
    {
        var item = await _service.CreateAsync(
            "Item", null, null, _space.ToDo, priorityId: _space.Low, spaceId: _space.Id);

        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.UpdateDetailsAsync(item.Id, Details("Item", null, null, Guid.NewGuid())));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync()).PriorityId, Is.EqualTo(_space.Low));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithNullPriorityId_ClearsIt()
    {
        var item = await _service.CreateAsync(
            "Item", null, null, _space.ToDo, priorityId: _space.Low, spaceId: _space.Id);

        var updated = (await _service.UpdateDetailsAsync(item.Id, Details("Item", null, null, null))).Item;

        Assert.That(updated.PriorityId, Is.Null);
    }

    [Test]
    public void UpdateDetailsAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.UpdateDetailsAsync(Guid.NewGuid(), Details("Title", null, null, null)));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithCurrentExpectedVersion_Succeeds()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        var updated = (await _service.UpdateDetailsAsync(
            item.Id, Details("Renamed", null, null, _space.High), expectedVersion: item.Version)).Item;

        Assert.That(updated.Title, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithStaleExpectedVersion_ThrowsAndLeavesItemUnchanged()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() => _service.UpdateDetailsAsync(
            item.Id, Details("Renamed", null, null, _space.High), expectedVersion: item.Version + 1));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync()).Title, Is.EqualTo("Item"));
    }

    [Test]
    public async Task RescheduleAsync_WithStaleExpectedVersion_Throws()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() => _service.RescheduleAsync(
            item.Id, new DateOnly(2026, 9, 25), null, expectedVersion: item.Version + 1));
    }

    [Test]
    public async Task SetTagsAsync_WithStaleExpectedVersion_Throws()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _service.SetTagsAsync(item.Id, Tags("urgent"), expectedVersion: item.Version + 1));
    }

    [Test]
    public async Task AssignAsync_WithAKnownUser_SetsAssignedToUserId()
    {
        var user = User.CreateHuman("alice", "alice");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        var assigned = (await _service.AssignAsync(item.Id, user.Id)).Item;

        Assert.That(assigned.AssignedToUserId, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task AssignAsync_WithNullUserId_ClearsTheAssignee()
    {
        var user = User.CreateHuman("alice", "alice");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);
        await _service.AssignAsync(item.Id, user.Id);

        var unassigned = (await _service.AssignAsync(item.Id, null)).Item;

        Assert.That(unassigned.AssignedToUserId, Is.Null);
    }

    [Test]
    public void AssignAsync_WithUnknownUserId_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.AssignAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public async Task AssignAsync_ToADeactivatedUser_IsAValidationError()
    {
        var user = User.CreateHuman("alice", "alice");
        user.Deactivate(DateTimeOffset.UtcNow);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<DomainValidationException>(() => _service.AssignAsync(item.Id, user.Id));
    }

    [Test]
    public async Task SetTagsAsync_SetsTheTagList()
    {
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        var tagged = (await _service.SetTagsAsync(item.Id, Tags("urgent", "needs review"))).Item;

        Assert.That(tagged.Tags, Is.EqualTo(new[] { "urgent", "needs review" }));
    }

    [Test]
    public async Task SetTagsAsync_WithEmptyList_ClearsTags()
    {
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);
        await _service.SetTagsAsync(item.Id, Tags("urgent"));

        var cleared = (await _service.SetTagsAsync(item.Id, Tags())).Item;

        Assert.That(cleared.Tags, Is.Empty);
    }

    [Test]
    public async Task SetTagsAsync_TrimsAndDeDuplicatesCaseInsensitively()
    {
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        var tagged = (await _service.SetTagsAsync(item.Id, Tags(" Bug ", "bug", "Feature"))).Item;

        Assert.That(tagged.Tags, Is.EqualTo(new[] { "Bug", "Feature" }));
    }

    [Test]
    public async Task SetTagsAsync_WithABlankTag_ThrowsInvalidWorkItemTagException()
    {
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<InvalidWorkItemTagException>(() =>
            _service.SetTagsAsync(item.Id, Tags("urgent", "   ")));
    }

    [Test]
    public void SetTagsAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.SetTagsAsync(Guid.NewGuid(), Tags("urgent")));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CreateAsync_WithBlankTitle_ThrowsDomainValidationException(string title)
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _service.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id));
    }

    [Test]
    public void CreateAsync_WithTitleOverMaxLength_ThrowsDomainValidationException()
    {
        var title = new string('a', WorkItem.TitleMaxLength + 1);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _service.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithBlankTitle_ThrowsAndLeavesTitleUnchanged()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _service.UpdateDetailsAsync(item.Id, Details(" ", null, null, null)));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync()).Title, Is.EqualTo("Item"));
    }

    [Test]
    public async Task SetTagsAsync_WithATagOverMaxLength_ThrowsDomainValidationException()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<DomainValidationException>(() =>
            _service.SetTagsAsync(item.Id, Tags(new string('t', WorkItem.TagMaxLength + 1))));
    }

    [Test]
    public async Task SetTagsAsync_WithMoreThanMaxTags_ThrowsDomainValidationException()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);
        var tags = Enumerable.Range(0, WorkItem.MaxTags + 1).Select(i => $"tag{i}").ToList();

        Assert.ThrowsAsync<DomainValidationException>(() => _service.SetTagsAsync(item.Id, Tags([.. tags])));
    }

    [Test]
    public async Task SetTagsAsync_CountsTagsAfterDeDuplication()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);
        var tags = Enumerable.Range(0, WorkItem.MaxTags).Select(i => $"tag{i}").Append("TAG0").ToList();

        var tagged = (await _service.SetTagsAsync(item.Id, Tags([.. tags]))).Item;

        Assert.That(tagged.Tags, Has.Count.EqualTo(WorkItem.MaxTags));
    }

    [Test]
    public async Task GetSwimlanesAsync_ReturnsEachLaneWithItsOwnCardsInRankOrder()
    {
        var scope = await _service.CreateAsync("Scope", null, null, _space.ToDo, spaceId: _space.Id);
        var laneA = await _service.CreateAsync("Lane A", null, scope.Id, _space.ToDo);
        var laneB = await _service.CreateAsync("Lane B", null, scope.Id, _space.ToDo, afterId: laneA.Id);
        var a1 = await _service.CreateAsync("A1", null, laneA.Id, _space.ToDo);
        var a2 = await _service.CreateAsync("A2", null, laneA.Id, _space.ToDo, afterId: a1.Id);
        var grandchild = await _service.CreateAsync("Too deep", null, a1.Id, _space.ToDo);

        var swimlanes = await _service.GetSwimlanesAsync(scope.Id);

        Assert.That(swimlanes.Select(s => s.Lane.Id), Is.EqualTo(new[] { laneA.Id, laneB.Id }));
        Assert.That(swimlanes[0].Cards.Select(c => c.Id), Is.EqualTo(new[] { a1.Id, a2.Id }));
        Assert.That(swimlanes[1].Cards, Is.Empty);
        Assert.That(swimlanes.SelectMany(s => s.Cards).Select(c => c.Id), Does.Not.Contain(grandchild.Id));
    }

    [Test]
    public async Task GetSwimlanesAsync_WithNoScope_UsesTopLevelItemsAsLanes()
    {
        var top = await _service.CreateAsync("Top", null, null, _space.ToDo, spaceId: _space.Id);
        var card = await _service.CreateAsync("Card", null, top.Id, _space.ToDo);

        var swimlanes = await _service.GetSwimlanesAsync(null, _space.Id);

        Assert.That(swimlanes.Single().Lane.Id, Is.EqualTo(top.Id));
        Assert.That(swimlanes.Single().Cards.Single().Id, Is.EqualTo(card.Id));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithOnlyATitle_KeepsTheOtherDetailsAndReportsTheTitleChanged()
    {
        var item = await _service.CreateAsync(
            "Item", "Keep me", null, _space.ToDo,
            typeId: _space.Task, priorityId: _space.High, spaceId: _space.Id);

        var result = await _service.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { Title = "Renamed" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Item.Title, Is.EqualTo("Renamed"));
            Assert.That(result.Item.Description, Is.EqualTo("Keep me"));
            Assert.That(result.Item.TypeId, Is.EqualTo(_space.Task));
            Assert.That(result.Item.PriorityId, Is.EqualTo(_space.High));
            Assert.That(result.ChangedFields, Is.EqualTo(new[] { WorkItemField.Title }));
        });
    }

    [Test]
    public async Task SetTagsAsync_LeftOut_LeavesTheTagsAlone()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);
        await _service.SetTagsAsync(item.Id, Tags("urgent"));

        var result = await _service.SetTagsAsync(item.Id, Optional<IReadOnlyList<string>>.Unset);

        Assert.Multiple(() =>
        {
            Assert.That(result.Item.Tags, Is.EqualTo(new[] { "urgent" }));
            Assert.That(result.ChangedFields, Is.Empty);
        });
    }

    [Test]
    public async Task ChangeStatusAsync_WithStaleExpectedVersion_ThrowsAndLeavesItemUnchanged()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _service.ChangeStatusAsync(item.Id, _space.Doing, expectedVersion: item.Version + 1));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync()).StatusId, Is.EqualTo(_space.ToDo));
    }

    [Test]
    public async Task ReparentAsync_WithStaleExpectedVersion_ThrowsAndLeavesItemUnchanged()
    {
        var parent = await _service.CreateAsync("Parent", null, null, _space.ToDo, spaceId: _space.Id);
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _service.ReparentAsync(item.Id, parent.Id, expectedVersion: item.Version + 1));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync(w => w.Id == item.Id)).ParentId, Is.Null);
    }

    [Test]
    public async Task AssignAsync_WithStaleExpectedVersion_ThrowsAndLeavesItemUnchanged()
    {
        var user = User.CreateHuman("alice", "alice");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var item = await _service.CreateAsync("Task", null, null, _space.ToDo, spaceId: _space.Id);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _service.AssignAsync(item.Id, user.Id, expectedVersion: item.Version + 1));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync()).AssignedToUserId, Is.Null);
    }

    [Test]
    public async Task ChangeStatusAsync_ReportsTheFieldsThatChanged()
    {
        var item = await _service.CreateAsync("Item", null, null, _space.ToDo, spaceId: _space.Id);

        var result = await _service.ChangeStatusAsync(item.Id, _space.Doing, expectedVersion: item.Version);

        Assert.That(result.ChangedFields, Does.Contain(WorkItemField.StatusId));
    }
}
