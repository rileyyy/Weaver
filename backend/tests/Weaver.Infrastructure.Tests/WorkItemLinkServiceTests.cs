using Microsoft.EntityFrameworkCore;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;
using Weaver.Infrastructure.Services;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class WorkItemLinkServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemLinkService _links = null!;
    private WorkItemService _workItems = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();
        _links = new WorkItemLinkService(_db);
        _workItems = new WorkItemService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_LinksTwoWorkItems()
    {
        var a = await _workItems.CreateAsync("A", null, null, StatusConfiguration.ToDoId);
        var b = await _workItems.CreateAsync("B", null, null, StatusConfiguration.ToDoId);

        var link = await _links.CreateAsync(a.Id, b.Id);

        Assert.That(link.WorkItemId, Is.EqualTo(a.Id));
        Assert.That(link.LinkedWorkItemId, Is.EqualTo(b.Id));
    }

    [Test]
    public async Task CreateAsync_WithSameId_ThrowsSelfWorkItemLinkException()
    {
        var a = await _workItems.CreateAsync("A", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<SelfWorkItemLinkException>(() => _links.CreateAsync(a.Id, a.Id));
    }

    [Test]
    public void CreateAsync_WithUnknownWorkItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _links.CreateAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Test]
    public async Task CreateAsync_WhenAlreadyLinked_ThrowsDuplicateWorkItemLinkException()
    {
        var a = await _workItems.CreateAsync("A", null, null, StatusConfiguration.ToDoId);
        var b = await _workItems.CreateAsync("B", null, null, StatusConfiguration.ToDoId);
        await _links.CreateAsync(a.Id, b.Id);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => _links.CreateAsync(a.Id, b.Id));
    }

    [Test]
    public async Task CreateAsync_WhenAlreadyLinkedInReverse_ThrowsDuplicateWorkItemLinkException()
    {
        var a = await _workItems.CreateAsync("A", null, null, StatusConfiguration.ToDoId);
        var b = await _workItems.CreateAsync("B", null, null, StatusConfiguration.ToDoId);
        await _links.CreateAsync(a.Id, b.Id);

        Assert.ThrowsAsync<DuplicateWorkItemLinkException>(() => _links.CreateAsync(b.Id, a.Id));
    }

    [Test]
    public async Task ListForWorkItemAsync_ReturnsLinksRegardlessOfWhichSideCreatedThem()
    {
        var a = await _workItems.CreateAsync("A", null, null, StatusConfiguration.ToDoId);
        var b = await _workItems.CreateAsync("B", null, null, StatusConfiguration.ToDoId);
        var c = await _workItems.CreateAsync("C", null, null, StatusConfiguration.ToDoId);
        await _links.CreateAsync(a.Id, b.Id);
        await _links.CreateAsync(c.Id, a.Id);

        var linksForA = await _links.ListForWorkItemAsync(a.Id);

        Assert.That(linksForA, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheLink()
    {
        var a = await _workItems.CreateAsync("A", null, null, StatusConfiguration.ToDoId);
        var b = await _workItems.CreateAsync("B", null, null, StatusConfiguration.ToDoId);
        var link = await _links.CreateAsync(a.Id, b.Id);

        await _links.DeleteAsync(link.Id);

        Assert.That(await _links.ListForWorkItemAsync(a.Id), Is.Empty);
    }

    [Test]
    public void DeleteAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _links.DeleteAsync(Guid.NewGuid()));
    }
}
