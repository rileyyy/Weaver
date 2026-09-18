using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;
using Weaver.Infrastructure.Services;
using Xunit;

namespace Weaver.Infrastructure.Tests;

public class WorkItemServiceTests
{
    private static WeaverDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new WeaverDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task CreateAsync_WithUnknownParent_ThrowsEntityNotFoundException()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CreateAsync("Task", null, parentId: Guid.NewGuid(), StatusConfiguration.ToDoId));
    }

    [Fact]
    public async Task CreateAsync_WithUnknownStatus_ThrowsEntityNotFoundException()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CreateAsync("Task", null, parentId: null, statusId: Guid.NewGuid()));
    }

    [Fact]
    public async Task ChangeStatusAsync_MovesColumn_ButNeverChangesParent()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var parent = await service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var child = await service.CreateAsync("Child", null, parent.Id, StatusConfiguration.ToDoId);

        var moved = await service.ChangeStatusAsync(child.Id, StatusConfiguration.DoingId);

        Assert.Equal(StatusConfiguration.DoingId, moved.StatusId);
        Assert.Equal(parent.Id, moved.ParentId);
    }

    [Fact]
    public async Task ReparentAsync_ChangesParent_ButNeverChangesStatus()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var oldParent = await service.CreateAsync("Old parent", null, null, StatusConfiguration.ToDoId);
        var newParent = await service.CreateAsync("New parent", null, null, StatusConfiguration.ToDoId);
        var child = await service.CreateAsync("Child", null, oldParent.Id, StatusConfiguration.DoingId);

        var moved = await service.ReparentAsync(child.Id, newParent.Id);

        Assert.Equal(newParent.Id, moved.ParentId);
        Assert.Equal(StatusConfiguration.DoingId, moved.StatusId);
    }

    [Fact]
    public async Task ReparentAsync_ToItself_ThrowsCyclicParentException()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var item = await service.CreateAsync("Item", null, null, StatusConfiguration.ToDoId);

        await Assert.ThrowsAsync<CyclicParentException>(() => service.ReparentAsync(item.Id, item.Id));
    }

    [Fact]
    public async Task ReparentAsync_ToOwnDescendant_ThrowsCyclicParentException()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var root = await service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        var grandchild = await service.CreateAsync("Grandchild", null, child.Id, StatusConfiguration.ToDoId);

        await Assert.ThrowsAsync<CyclicParentException>(() => service.ReparentAsync(root.Id, grandchild.Id));
    }

    [Fact]
    public async Task ReparentAsync_ToUnrelatedItem_Succeeds()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var root = await service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        var unrelated = await service.CreateAsync("Unrelated", null, null, StatusConfiguration.ToDoId);

        var moved = await service.ReparentAsync(child.Id, unrelated.Id);

        Assert.Equal(unrelated.Id, moved.ParentId);
    }

    [Fact]
    public async Task DeleteAsync_WithChildren_WithoutCascade_Throws()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var parent = await service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        await service.CreateAsync("Child", null, parent.Id, StatusConfiguration.ToDoId);

        await Assert.ThrowsAsync<WorkItemHasChildrenException>(() => service.DeleteAsync(parent.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithChildren_WithCascade_DeletesWholeSubtree()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var root = await service.CreateAsync("Root", null, null, StatusConfiguration.ToDoId);
        var child = await service.CreateAsync("Child", null, root.Id, StatusConfiguration.ToDoId);
        await service.CreateAsync("Grandchild", null, child.Id, StatusConfiguration.ToDoId);

        await service.DeleteAsync(root.Id, cascade: true);

        Assert.Empty(await db.WorkItems.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_Leaf_DoesNotRequireCascade()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var item = await service.CreateAsync("Leaf", null, null, StatusConfiguration.ToDoId);

        await service.DeleteAsync(item.Id);

        Assert.Empty(await db.WorkItems.ToListAsync());
    }

    [Fact]
    public async Task ChangeStatusAsync_WithAfterId_OrdersCardBetweenNeighborsInDestinationCell()
    {
        using var db = CreateContext();
        var service = new WorkItemService(db);

        var parent = await service.CreateAsync("Parent", null, null, StatusConfiguration.ToDoId);
        var first = await service.CreateAsync("First", null, parent.Id, StatusConfiguration.DoingId);
        var second = await service.CreateAsync("Second", null, parent.Id, StatusConfiguration.DoingId, afterId: first.Id);
        var moving = await service.CreateAsync("Moving", null, parent.Id, StatusConfiguration.ToDoId);

        var moved = await service.ChangeStatusAsync(moving.Id, StatusConfiguration.DoingId, afterId: first.Id);

        Assert.True(moved.Rank > first.Rank);
        Assert.True(moved.Rank < second.Rank);
    }
}
