using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class QuickPickServiceTests
{
    private WeaverDbContext _db = null!;

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<QuickPickService> ServiceAt(DateTimeOffset now, string timeZoneId)
    {
        var clock = new FixedTimeProvider(now);
        _db = TestDatabase.Create(clock);
        var settings = await _db.ServerSettings.SingleAsync();
        settings.SetTimeZone(timeZoneId);
        await _db.SaveChangesAsync();

        var calendar = new ServerCalendar(new ServerSettingsService(_db, new AdminAuthorizer(_db)), clock);
        return new QuickPickService(calendar);
    }

    [Test]
    public async Task List_ResolvesEveryPick_InTheServerTimeZone_NotUtc()
    {
        // Saturday 23:30 UTC is already Sunday in Berlin, so "this weekend" is only Sunday
        // and "next week" starts the next day.
        var service = await ServiceAt(new DateTimeOffset(2026, 11, 7, 23, 30, 0, TimeSpan.Zero), "Europe/Berlin");

        var picks = await service.ListAsync();

        Assert.That(picks, Is.EqualTo(new[]
        {
            new ResolvedQuickPick(QuickPick.ThisWeekend, new DateRange(new(2026, 11, 8), new(2026, 11, 8))),
            new ResolvedQuickPick(QuickPick.NextWeek, new DateRange(new(2026, 11, 9), new(2026, 11, 15))),
            new ResolvedQuickPick(QuickPick.NextMonth, new DateRange(new(2026, 12, 1), new(2026, 12, 31))),
        }));
    }

    [Test]
    public async Task List_OnTheLastEveningOfTheMonthInUtc_UsesTheServerZonesNewMonth()
    {
        // 30 Nov 23:30 UTC is 1 Dec in Berlin, so "next month" is January.
        var service = await ServiceAt(new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero), "Europe/Berlin");

        var picks = await service.ListAsync();

        Assert.That(
            picks.Single(p => p.Pick == QuickPick.NextMonth).Dates,
            Is.EqualTo(new DateRange(new(2027, 1, 1), new(2027, 1, 31))));
    }
}
