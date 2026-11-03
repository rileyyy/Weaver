namespace Weaver.Domain.Tests;

[TestFixture]
public class QuickPickTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    // 2026-11-04 is a Wednesday.
    [Test]
    public void ThisWeekend_MidWeek_IsTheComingSaturdayAndSunday()
    {
        var range = QuickPickDates.Resolve(QuickPick.ThisWeekend, D(2026, 11, 4));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 11, 7), D(2026, 11, 8))));
    }

    [Test]
    public void ThisWeekend_OnASaturday_IsTodayAndTomorrow()
    {
        var range = QuickPickDates.Resolve(QuickPick.ThisWeekend, D(2026, 11, 7));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 11, 7), D(2026, 11, 8))));
    }

    [Test]
    public void ThisWeekend_OnASunday_IsJustToday_NotNextWeekend()
    {
        var range = QuickPickDates.Resolve(QuickPick.ThisWeekend, D(2026, 11, 8));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 11, 8), D(2026, 11, 8))));
    }

    [Test]
    public void ThisWeekend_SpanningTheEndOfAMonth_CrossesIntoTheNextMonth()
    {
        // 2026-10-29 is a Thursday; that weekend is Sat 31 Oct and Sun 1 Nov.
        var range = QuickPickDates.Resolve(QuickPick.ThisWeekend, D(2026, 10, 29));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 10, 31), D(2026, 11, 1))));
    }

    [Test]
    public void NextWeek_MidWeek_IsTheFollowingMondayToSunday()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextWeek, D(2026, 11, 4));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 11, 9), D(2026, 11, 15))));
    }

    [Test]
    public void NextWeek_OnAMonday_SkipsTheWeekThatJustStarted()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextWeek, D(2026, 11, 2));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 11, 9), D(2026, 11, 15))));
    }

    [Test]
    public void NextWeek_OnASunday_StartsTomorrow()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextWeek, D(2026, 11, 8));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 11, 9), D(2026, 11, 15))));
    }

    [Test]
    public void NextWeek_AcrossTheNewYear_RunsIntoJanuary()
    {
        // 2026-12-30 is a Wednesday.
        var range = QuickPickDates.Resolve(QuickPick.NextWeek, D(2026, 12, 30));

        Assert.That(range, Is.EqualTo(new DateRange(D(2027, 1, 4), D(2027, 1, 10))));
    }

    [Test]
    public void NextMonth_IsTheWholeOfTheFollowingCalendarMonth()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextMonth, D(2026, 11, 4));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 12, 1), D(2026, 12, 31))));
    }

    [Test]
    public void NextMonth_OnTheLastDayOfAMonth_IsTheMonthAfter()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextMonth, D(2026, 1, 31));

        Assert.That(range, Is.EqualTo(new DateRange(D(2026, 2, 1), D(2026, 2, 28))));
    }

    [Test]
    public void NextMonth_InDecember_IsJanuaryOfTheNextYear()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextMonth, D(2026, 12, 15));

        Assert.That(range, Is.EqualTo(new DateRange(D(2027, 1, 1), D(2027, 1, 31))));
    }

    [Test]
    public void NextMonth_IntoALeapFebruary_EndsOnThe29th()
    {
        var range = QuickPickDates.Resolve(QuickPick.NextMonth, D(2028, 1, 10));

        Assert.That(range, Is.EqualTo(new DateRange(D(2028, 2, 1), D(2028, 2, 29))));
    }
}
