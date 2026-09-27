using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class StatusServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private StatusService _statuses = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _statuses = new StatusService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededStatusesOrderedByOrder()
    {
        var statuses = await _statuses.GetAllAsync();

        Assert.That(statuses.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
    }
}
