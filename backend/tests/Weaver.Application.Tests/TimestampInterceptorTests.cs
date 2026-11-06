using Weaver.Domain;

namespace Weaver.Application.Tests;

[TestFixture]
public class TimestampInterceptorTests
{
    // 1234567 ticks past the second: 123456 whole microseconds and 7 ticks Postgres can't store.
    private static readonly DateTimeOffset Now =
        new DateTimeOffset(2026, 11, 6, 8, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

    private static readonly DateTimeOffset NowInMicroseconds =
        new DateTimeOffset(2026, 11, 6, 8, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    [Test]
    public async Task OnInsert_StampsTheClocksTimeCutToWholeMicroseconds()
    {
        await using var db = TestDatabase.Create(new FixedTimeProvider(Now));
        var user = User.CreateHuman("ada", "ADA");
        db.Users.Add(user);

        await db.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(user.CreatedAtUtc, Is.EqualTo(NowInMicroseconds));
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(NowInMicroseconds));
        });
    }

    [Test]
    public async Task OnUpdate_StampsOnlyUpdatedAt_CutToWholeMicroseconds()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero));
        await using var db = TestDatabase.Create(clock);
        var user = User.CreateHuman("ada", "ADA");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var created = user.CreatedAtUtc;

        clock.Advance(Now - clock.GetUtcNow());
        user.Rename("grace", "GRACE");
        await db.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(user.CreatedAtUtc, Is.EqualTo(created));
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(NowInMicroseconds));
        });
    }
}
