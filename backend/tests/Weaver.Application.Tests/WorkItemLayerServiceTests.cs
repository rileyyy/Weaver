using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Application.Tests;

/// <summary>The shared lookup rules are covered in <see cref="WorkItemPriorityServiceTests"/>.</summary>
[TestFixture]
public class WorkItemLayerServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemLayerService _layers = null!;
    private User _admin = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _layers = new WorkItemLayerService(_db, new AdminAuthorizer(_db));
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededLayersOrderedByOrder()
    {
        var layers = await _layers.GetAllAsync();

        Assert.That(layers.Select(l => l.Name), Is.EqualTo(new[] { "Project", "Goal", "Task" }));
    }

    [Test]
    public async Task CreateAndRename_Work()
    {
        var created = await _layers.CreateAsync(_admin.Id, "Epic");
        await _layers.RenameAsync(_admin.Id, created.Id, "Initiative");

        var layers = await _layers.GetAllAsync();
        Assert.That(layers.Select(l => l.Name), Is.EqualTo(new[] { "Project", "Goal", "Task", "Initiative" }));
    }

    [Test]
    public void CreateAsync_ByANonAdmin_IsForbidden()
    {
        var member = TestUsers.Add(_db, "member");

        Assert.ThrowsAsync<AdminRequiredException>(() => _layers.CreateAsync(member.Id, "Epic"));
    }

    [Test]
    public async Task DeleteAsync_WithAReplacement_MovesItsItemsToIt()
    {
        var item = AddItem(WorkItemLayerConfiguration.GoalId);

        await _layers.DeleteAsync(_admin.Id, WorkItemLayerConfiguration.GoalId, WorkItemLayerConfiguration.TaskId);

        Assert.That((await Reload(item)).LayerId, Is.EqualTo(WorkItemLayerConfiguration.TaskId));
    }

    [Test]
    public async Task DeleteAsync_WithoutAReplacement_LeavesItsItemsWithNoLayer()
    {
        var item = AddItem(WorkItemLayerConfiguration.GoalId);

        await _layers.DeleteAsync(_admin.Id, WorkItemLayerConfiguration.GoalId, replacementId: null);

        Assert.That((await Reload(item)).LayerId, Is.Null);
    }

    private WorkItem AddItem(Guid layerId)
    {
        var item = WorkItem.Create("Item", null, null, StatusConfiguration.ToDoId, 1, layerId: layerId);
        _db.WorkItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Task<WorkItem> Reload(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
}
