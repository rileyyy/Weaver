using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class StatusServiceTests
{
    private WeaverDbContext _db = null!;
    private StatusService _statuses = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();
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
