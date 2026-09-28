using Weaver.Application.Services;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class UserServiceTests
{
    private WeaverDbContext _db = null!;
    private UserService _users = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _users = new UserService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task ListAsync_LeavesOutDeactivatedUsers_SoPickersDoNotOfferThem()
    {
        TestUsers.Add(_db, "alice");
        var bob = TestUsers.Add(_db, "bob");
        bob.Deactivate(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync();

        var users = await _users.ListAsync();

        Assert.That(users.Select(u => u.Username), Is.EqualTo(new[] { "alice" }));
    }
}
