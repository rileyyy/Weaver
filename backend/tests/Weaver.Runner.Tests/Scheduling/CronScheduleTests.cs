using Weaver.Runner.Scheduling;

namespace Weaver.Runner.Tests.Scheduling;

[TestFixture]
public class CronScheduleTests
{
    [Test]
    public void Next_IsInTheSchedulesTimeZone()
    {
        var schedule = CronSchedule.Parse("0 3 * * *", TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));

        Assert.That(schedule.Next(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero)),
            Is.EqualTo(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void Next_IsStrictlyAfterNow_SoARunDoesNotRepeat()
    {
        var schedule = CronSchedule.Parse("0 3 * * *", TimeZoneInfo.Utc);

        Assert.That(schedule.Next(new DateTimeOffset(2026, 11, 11, 3, 0, 0, TimeSpan.Zero)),
            Is.EqualTo(new DateTimeOffset(2026, 11, 12, 3, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void AnInvalidExpression_IsRefused()
    {
        Assert.That(CronSchedule.TryParse("every night", TimeZoneInfo.Utc, out _), Is.False);
    }
}
