using Microsoft.EntityFrameworkCore;
using Weaver.Infrastructure.Services;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class WorkItemLayerServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemLayerService _layers = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();
        _layers = new WorkItemLayerService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededLayersOrderedByOrder()
    {
        var layers = await _layers.GetAllAsync();

        Assert.That(layers.Select(l => l.Name), Is.EqualTo(new[] { "Project", "Goal", "Task" }));
    }
}
