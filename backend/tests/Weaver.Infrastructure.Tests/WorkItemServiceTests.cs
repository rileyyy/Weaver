using Microsoft.EntityFrameworkCore;
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
}
