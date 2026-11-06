using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class ServerSettingsTests
{
    [Test]
    public void CreateDefault_UsesUtc()
    {
        Assert.That(ServerSettings.CreateDefault().TimeZoneId, Is.EqualTo("UTC"));
    }

    [Test]
    public void SetTimeZone_AcceptsAnIanaIdAndTrimsIt()
    {
        var settings = ServerSettings.CreateDefault();

        settings.SetTimeZone("  Asia/Tokyo ");

        Assert.That(settings.TimeZoneId, Is.EqualTo("Asia/Tokyo"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Mars/Olympus_Mons")]
    [TestCase("W. Europe Standard Time")]
    public void SetTimeZone_RejectsAnythingButAKnownIanaId(string timeZoneId)
    {
        var settings = ServerSettings.CreateDefault();

        Assert.Throws<DomainValidationException>(() => settings.SetTimeZone(timeZoneId));
        Assert.That(settings.TimeZoneId, Is.EqualTo("UTC"));
    }

    [Test]
    public void SetTimeZone_RejectsAnIdLongerThanTheColumn()
    {
        var settings = ServerSettings.CreateDefault();

        Assert.Throws<DomainValidationException>(() =>
            settings.SetTimeZone(new string('a', ServerSettings.TimeZoneIdMaxLength + 1)));
    }

    [Test]
    public void CreateDefault_KeepsTrashForThirtyDays()
    {
        Assert.That(ServerSettings.CreateDefault().TrashRetentionDays, Is.EqualTo(30));
    }

    [TestCase(1)]
    [TestCase(90)]
    [TestCase(3650)]
    public void SetTrashRetention_AcceptsOneDayToTenYears(int days)
    {
        var settings = ServerSettings.CreateDefault();

        settings.SetTrashRetention(days);

        Assert.That(settings.TrashRetentionDays, Is.EqualTo(days));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(3651)]
    public void SetTrashRetention_RejectsAnythingOutsideOneDayToTenYears(int days)
    {
        var settings = ServerSettings.CreateDefault();

        Assert.Throws<DomainValidationException>(() => settings.SetTrashRetention(days));
        Assert.That(settings.TrashRetentionDays, Is.EqualTo(30));
    }

    // Elapsed time, not calendar days in the server time zone: a delete at 14:30 with two
    // days' retention expires at 14:30 two days later.
    [Test]
    public void TrashExpiryCutoff_IsTheRetentionPeriodBeforeNow()
    {
        var settings = ServerSettings.CreateDefault();
        settings.SetTrashRetention(2);
        var now = new DateTimeOffset(2026, 3, 12, 14, 30, 0, TimeSpan.Zero);

        Assert.That(settings.TrashExpiryCutoff(now), Is.EqualTo(new DateTimeOffset(2026, 3, 10, 14, 30, 0, TimeSpan.Zero)));
    }

    // 14:30Z and 15:30Z share a UTC date but fall either side of midnight in Tokyo (UTC+9).
    [TestCase("2026-03-10T14:30:00Z", "2026-03-10")]
    [TestCase("2026-03-10T15:30:00Z", "2026-03-11")]
    public void DateAt_IsTheCalendarDateInTheServerTimeZone(string instant, string expected)
    {
        var settings = ServerSettings.CreateDefault();
        settings.SetTimeZone("Asia/Tokyo");

        Assert.That(settings.DateAt(DateTimeOffset.Parse(instant)), Is.EqualTo(DateOnly.Parse(expected)));
    }
}
