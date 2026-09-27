using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemLayerServiceTests
{
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
    private WeaverDbContext _db = null!;
    private WorkItemLayerService _layers = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(_clock);
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
