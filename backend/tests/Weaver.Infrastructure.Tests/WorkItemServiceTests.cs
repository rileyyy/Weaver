using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;
using Weaver.Infrastructure.Services;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class WorkItemServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();
        _service = new WorkItemService(_db);
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
            _service.CreateAsync("Task", null, parentId: Guid.NewGuid(), StatusConfiguration.ToDoId));
    }

    [Test]
    public void CreateAsync_WithUnknownStatus_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Task", null, parentId: null, statusId: Guid.NewGuid()));
    }

    [Test]
    public async Task ChangeStatusAsync_MovesColumn_ButNeverChangesParent()
    {
        var parent = await _service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, parent.Id, StatusConfiguration.ToDoId);

        var moved = await _service.ChangeStatusAsync(child.Id, StatusConfiguration.DoingId);

        Assert.That(moved.StatusId, Is.EqualTo(StatusConfiguration.DoingId));
        Assert.That(moved.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task ReparentAsync_ChangesParent_ButNeverChangesStatus()
    {
        var oldParent = await _service.CreateAsync("Old parent", null, null, StatusConfiguration.ToDoId);
        var newParent = await _service.CreateAsync("New parent", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, oldParent.Id, StatusConfiguration.DoingId);

        var moved = await _service.ReparentAsync(child.Id, newParent.Id);

        Assert.That(moved.ParentId, Is.EqualTo(newParent.Id));
        Assert.That(moved.StatusId, Is.EqualTo(StatusConfiguration.DoingId));
    }

    [Test]
    public async Task ReparentAsync_ToItself_ThrowsCyclicParentException()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<CyclicParentException>(() => _service.ReparentAsync(item.Id, item.Id));
    }

    [Test]
    public async Task ReparentAsync_ToOwnDescendant_ThrowsCyclicParentException()
    {
        var root = await _service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        var grandchild = await _service.CreateAsync("Grandchild", null, child.Id, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<CyclicParentException>(() => _service.ReparentAsync(root.Id, grandchild.Id));
    }

    [Test]
    public async Task ReparentAsync_ToUnrelatedItem_Succeeds()
    {
        var root = await _service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        var unrelated = await _service.CreateAsync("Unrelated", null, null, StatusConfiguration.ToDoId);

        var moved = await _service.ReparentAsync(child.Id, unrelated.Id);

        Assert.That(moved.ParentId, Is.EqualTo(unrelated.Id));
    }

    [Test]
    public async Task DeleteAsync_WithChildren_WithoutCascade_Throws()
    {
        var parent = await _service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        await _service.CreateAsync("Child", null, parent.Id, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<WorkItemHasChildrenException>(() => _service.DeleteAsync(parent.Id));
    }

    [Test]
    public async Task DeleteAsync_WithChildren_WithCascade_DeletesWholeSubtree()
    {
        var root = await _service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        await _service.CreateAsync("Grandchild", null, child.Id, StatusConfiguration.ToDoId);

        await _service.DeleteAsync(root.Id, cascade: true);

        Assert.That(await _db.WorkItems.ToListAsync(), Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_Leaf_DoesNotRequireCascade()
    {
        var item = await _service.CreateAsync("Leaf", null, null, StatusConfiguration.ToDoId);

        await _service.DeleteAsync(item.Id);

        Assert.That(await _db.WorkItems.ToListAsync(), Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_ItemWithLinksOnEitherSide_RemovesThoseLinks()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);
        var linkedFrom = await _service.CreateAsync("Linked from", null, null, StatusConfiguration.ToDoId);
        var linkedTo = await _service.CreateAsync("Linked to", null, null, StatusConfiguration.ToDoId);
        await AddLinkAsync(item.Id, linkedTo.Id);
        await AddLinkAsync(linkedFrom.Id, item.Id);
        var unrelatedLink = await AddLinkAsync(linkedFrom.Id, linkedTo.Id);

        await _service.DeleteAsync(item.Id);

        var remainingLinks = await _db.WorkItemLinks.ToListAsync();
        Assert.That(remainingLinks.Select(l => l.Id), Is.EqualTo(new[] { unrelatedLink.Id }));
        Assert.That(await _db.WorkItems.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_WithCascade_RemovesLinksOfDescendants()
    {
        var root = await _service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        var grandchild = await _service.CreateAsync("Grandchild", null, child.Id, StatusConfiguration.ToDoId);
        var outside = await _service.CreateAsync("Outside", null, null, StatusConfiguration.ToDoId);
        await AddLinkAsync(outside.Id, grandchild.Id);

        await _service.DeleteAsync(root.Id, cascade: true);

        Assert.That(await _db.WorkItemLinks.ToListAsync(), Is.Empty);
        Assert.That((await _db.WorkItems.SingleAsync()).Id, Is.EqualTo(outside.Id));
    }

    [Test]
    public async Task DeleteAsync_ItemIsBoardScope_ThrowsAndDeletesNothing()
    {
        var item = await _service.CreateAsync("Scope", null, null, StatusConfiguration.ToDoId);
        var other = await _service.CreateAsync("Other", null, null, StatusConfiguration.ToDoId);
        await AddLinkAsync(item.Id, other.Id);
        var board = await AddBoardAsync(item.Id);

        var thrown = Assert.ThrowsAsync<WorkItemIsBoardScopeException>(() => _service.DeleteAsync(item.Id));

        Assert.That(thrown!.BoardId, Is.EqualTo(board.Id));
        Assert.That(await _db.WorkItems.CountAsync(), Is.EqualTo(2));
        Assert.That(await _db.WorkItemLinks.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteAsync_WithCascade_DescendantIsBoardScope_Throws()
    {
        var root = await _service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        await AddBoardAsync(child.Id);

        Assert.ThrowsAsync<WorkItemIsBoardScopeException>(() => _service.DeleteAsync(root.Id, cascade: true));
        Assert.That(await _db.WorkItems.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_BoardScopedElsewhere_DoesNotBlockDelete()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);
        var scope = await _service.CreateAsync("Scope", null, null, StatusConfiguration.ToDoId);
        await AddBoardAsync(scope.Id);
        await AddBoardAsync(scopeItemId: null);

        await _service.DeleteAsync(item.Id);

        Assert.That((await _db.WorkItems.SingleAsync()).Id, Is.EqualTo(scope.Id));
    }

    private async Task<WorkItemLink> AddLinkAsync(Guid workItemId, Guid linkedWorkItemId)
    {
        var link = new WorkItemLink
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            LinkedWorkItemId = linkedWorkItemId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        _db.WorkItemLinks.Add(link);
        await _db.SaveChangesAsync();
        return link;
    }

    private async Task<Board> AddBoardAsync(Guid? scopeItemId)
    {
        var board = new Board { Id = Guid.NewGuid(), Name = "Board", ScopeItemId = scopeItemId };
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();
        return board;
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
        var parent = await _service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var first = await _service.CreateAsync("First", null, parent.Id, StatusConfiguration.ToDoId);
        var second = await _service.CreateAsync("Second", null, parent.Id, StatusConfiguration.ToDoId, afterId: first.Id);
        await _service.CreateAsync("Grandchild", null, second.Id, StatusConfiguration.ToDoId);

        var children = await _service.GetChildrenAsync(parent.Id);

        Assert.That(children.Select(c => c.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public async Task GetAllAsync_ReturnsEveryItem_RegardlessOfParent()
    {
        var root = await _service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await _service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        var grandchild = await _service.CreateAsync("Grandchild", null, child.Id, StatusConfiguration.ToDoId);
        var unrelated = await _service.CreateAsync("Unrelated", null, null, StatusConfiguration.ToDoId);

        var all = await _service.GetAllAsync();

        Assert.That(
            all.Select(w => w.Id),
            Is.EquivalentTo(new[] { root.Id, child.Id, grandchild.Id, unrelated.Id }));
    }

    [Test]
    public async Task RescheduleAsync_SetsStartAndEndDate_WithoutTouchingStatusOrParent()
    {
        var parent = await _service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var item = await _service.CreateAsync("Item", null, parent.Id, StatusConfiguration.ToDoId);
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(7);

        var rescheduled = await _service.RescheduleAsync(item.Id, start, end);

        Assert.That(rescheduled.StartDate, Is.EqualTo(start));
        Assert.That(rescheduled.EndDate, Is.EqualTo(end));
        Assert.That(rescheduled.StatusId, Is.EqualTo(StatusConfiguration.ToDoId));
        Assert.That(rescheduled.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task RescheduleAsync_WithNullDates_ClearsAnExistingSchedule()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);
        await _service.RescheduleAsync(item.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        var cleared = await _service.RescheduleAsync(item.Id, null, null);

        Assert.That(cleared.StartDate, Is.Null);
        Assert.That(cleared.EndDate, Is.Null);
    }

    [Test]
    public void RescheduleAsync_WithStartAfterEnd_ThrowsInvalidWorkItemScheduleException()
    {
        var itemId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(-1);

        Assert.ThrowsAsync<InvalidWorkItemScheduleException>(() =>
            _service.RescheduleAsync(itemId, start, end));
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
        var parent = await _service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var first = await _service.CreateAsync("First", null, parent.Id, StatusConfiguration.DoingId);
        var second = await _service.CreateAsync("Second", null, parent.Id, StatusConfiguration.DoingId, afterId: first.Id);
        var moving = await _service.CreateAsync("Moving", null, parent.Id, StatusConfiguration.ToDoId);

        var moved = await _service.ChangeStatusAsync(moving.Id, StatusConfiguration.DoingId, afterId: first.Id);

        Assert.That(moved.Rank, Is.GreaterThan(first.Rank));
        Assert.That(moved.Rank, Is.LessThan(second.Rank));
    }

    [Test]
    public async Task CreateAsync_WithLayerId_SetsTheLayer()
    {
        var item = await _service.CreateAsync(
            "Task", null, null, StatusConfiguration.ToDoId, layerId: WorkItemLayerConfiguration.TaskId);

        Assert.That(item.LayerId, Is.EqualTo(WorkItemLayerConfiguration.TaskId));
    }

    [Test]
    public void CreateAsync_WithUnknownLayerId_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, layerId: Guid.NewGuid()));
    }

    [Test]
    public async Task CreateAsync_DefaultsPriorityToMedium()
    {
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        Assert.That(item.Priority, Is.EqualTo(WorkItemPriority.Medium));
    }

    [Test]
    public async Task UpdateDetailsAsync_UpdatesTitleDescriptionLayerAndPriority()
    {
        var item = await _service.CreateAsync("Old title", "Old description", null, StatusConfiguration.ToDoId);

        var updated = await _service.UpdateDetailsAsync(
            item.Id, "New title", "New description", WorkItemLayerConfiguration.GoalId, WorkItemPriority.Urgent);

        Assert.That(updated.Title, Is.EqualTo("New title"));
        Assert.That(updated.Description, Is.EqualTo("New description"));
        Assert.That(updated.LayerId, Is.EqualTo(WorkItemLayerConfiguration.GoalId));
        Assert.That(updated.Priority, Is.EqualTo(WorkItemPriority.Urgent));
    }

    [Test]
    public async Task UpdateDetailsAsync_NeverChangesStatusOrParent()
    {
        var parent = await _service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var item = await _service.CreateAsync("Child", null, parent.Id, StatusConfiguration.DoingId);

        var updated = await _service.UpdateDetailsAsync(item.Id, "Renamed", null, null, WorkItemPriority.Low);

        Assert.That(updated.StatusId, Is.EqualTo(StatusConfiguration.DoingId));
        Assert.That(updated.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public void UpdateDetailsAsync_WithUnknownLayerId_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.UpdateDetailsAsync(Guid.NewGuid(), "Title", null, Guid.NewGuid(), WorkItemPriority.Medium));
    }

    [Test]
    public void UpdateDetailsAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.UpdateDetailsAsync(Guid.NewGuid(), "Title", null, null, WorkItemPriority.Medium));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithCurrentExpectedVersion_Succeeds()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);

        var updated = await _service.UpdateDetailsAsync(
            item.Id, "Renamed", null, null, WorkItemPriority.High, expectedVersion: item.Version);

        Assert.That(updated.Title, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task UpdateDetailsAsync_WithStaleExpectedVersion_ThrowsAndLeavesItemUnchanged()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() => _service.UpdateDetailsAsync(
            item.Id, "Renamed", null, null, WorkItemPriority.High, expectedVersion: item.Version + 1));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.WorkItems.SingleAsync()).Title, Is.EqualTo("Item"));
    }

    [Test]
    public async Task RescheduleAsync_WithStaleExpectedVersion_Throws()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() => _service.RescheduleAsync(
            item.Id, DateTimeOffset.UtcNow, null, expectedVersion: item.Version + 1));
    }

    [Test]
    public async Task SetTagsAsync_WithStaleExpectedVersion_Throws()
    {
        var item = await _service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<WorkItemVersionConflictException>(() =>
            _service.SetTagsAsync(item.Id, ["urgent"], expectedVersion: item.Version + 1));
    }

    [Test]
    public async Task AssignAsync_WithAKnownUser_SetsAssignedToUserId()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            NormalizedUsername = "alice",
            PasswordHash = "hash",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        var assigned = await _service.AssignAsync(item.Id, user.Id);

        Assert.That(assigned.AssignedToUserId, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task AssignAsync_WithNullUserId_ClearsTheAssignee()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            NormalizedUsername = "alice",
            PasswordHash = "hash",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        await _service.AssignAsync(item.Id, user.Id);

        var unassigned = await _service.AssignAsync(item.Id, null);

        Assert.That(unassigned.AssignedToUserId, Is.Null);
    }

    [Test]
    public void AssignAsync_WithUnknownUserId_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.AssignAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public async Task SetTagsAsync_SetsTheTagList()
    {
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        var tagged = await _service.SetTagsAsync(item.Id, ["urgent", "needs review"]);

        Assert.That(tagged.Tags, Is.EqualTo(new[] { "urgent", "needs review" }));
    }

    [Test]
    public async Task SetTagsAsync_WithEmptyList_ClearsTags()
    {
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        await _service.SetTagsAsync(item.Id, ["urgent"]);

        var cleared = await _service.SetTagsAsync(item.Id, []);

        Assert.That(cleared.Tags, Is.Empty);
    }

    [Test]
    public async Task SetTagsAsync_TrimsAndDeDuplicatesCaseInsensitively()
    {
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        var tagged = await _service.SetTagsAsync(item.Id, [" Bug ", "bug", "Feature"]);

        Assert.That(tagged.Tags, Is.EqualTo(new[] { "Bug", "Feature" }));
    }

    [Test]
    public async Task SetTagsAsync_WithABlankTag_ThrowsInvalidWorkItemTagException()
    {
        var item = await _service.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<InvalidWorkItemTagException>(() =>
            _service.SetTagsAsync(item.Id, ["urgent", "   "]));
    }

    [Test]
    public void SetTagsAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.SetTagsAsync(Guid.NewGuid(), ["urgent"]));
    }
}
