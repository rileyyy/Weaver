using Weaver.Application.Services;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemPriorityServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemPriorityService _priorities = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _priorities = new WorkItemPriorityService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededPrioritiesFromLeastToMostUrgent()
    {
        var priorities = await _priorities.GetAllAsync();

        Assert.That(priorities.Select(p => p.Name), Is.EqualTo(new[] { "Low", "Medium", "High", "Urgent" }));
    }
}
