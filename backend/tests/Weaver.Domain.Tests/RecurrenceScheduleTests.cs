using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class RecurrenceScheduleTests
{
    // 2026-09-28 is a Monday.
    private static readonly DateOnly Monday = new(2026, 9, 28);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Test]
    public void Create_WeekBasedWithoutDays_Throws()
    {
        var thrown = Assert.Throws<DomainValidationException>(() =>
            RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, [], Monday, null));

        Assert.That(thrown!.Message, Does.Contain("at least one day"));
    }

    [Test]
    public void Create_WithEndBeforeStart_Throws()
    {
        Assert.Throws<DomainValidationException>(() =>
            RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, [], Monday, Monday.AddDays(-1)));
    }

    [Test]
    public void Create_WithEndOnStart_IsAllowed()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, [], Monday, Monday);

        Assert.That(schedule.EndDate, Is.EqualTo(Monday));
    }

    [Test]
    public void Create_WithUndefinedFrequency_Throws()
    {
        Assert.Throws<DomainValidationException>(() =>
            RecurrenceSchedule.Create((RecurrenceFrequency)99, [DayOfWeek.Monday], Monday, null));
    }

    [TestCase(RecurrenceFrequency.Monthly)]
    [TestCase(RecurrenceFrequency.Quarterly)]
    [TestCase(RecurrenceFrequency.Yearly)]
    public void Create_MonthBased_DropsDaysAndNeedsNone(RecurrenceFrequency frequency)
    {
        var withDays = RecurrenceSchedule.Create(frequency, [DayOfWeek.Friday], Monday, null);
        var withoutDays = RecurrenceSchedule.Create(frequency, [], Monday, null);

        Assert.That(withDays.Days, Is.EqualTo(RecurrenceDays.None));
        Assert.That(withoutDays.Days, Is.EqualTo(RecurrenceDays.None));
    }

    [Test]
    public void DaysOfWeek_AreReturnedMondayFirst()
    {
        var schedule = RecurrenceSchedule.Create(
            RecurrenceFrequency.Weekly,
            [DayOfWeek.Sunday, DayOfWeek.Wednesday, DayOfWeek.Monday],
            Monday,
            null);

        Assert.That(schedule.DaysOfWeek, Is.EqualTo(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Sunday }));
    }

    [Test]
    public void Weekly_FallsOnEveryChosenDay()
    {
        var schedule = RecurrenceSchedule.Create(
            RecurrenceFrequency.Weekly, [DayOfWeek.Monday, DayOfWeek.Friday], Monday, null);

        var occurrences = schedule.OccurrencesBetween(Monday, Monday.AddDays(13));

        Assert.That(occurrences, Is.EqualTo(new[] { D(2026, 9, 28), D(2026, 10, 2), D(2026, 10, 5), D(2026, 10, 9) }));
    }

    [Test]
    public void Weekly_IncludesSundayOfTheStartWeek()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, [DayOfWeek.Sunday], Monday, null);

        Assert.That(schedule.OccurrencesBetween(Monday, Monday.AddDays(6)), Is.EqualTo(new[] { D(2026, 10, 4) }));
    }

    [Test]
    public void BiWeekly_SkipsEveryOtherWeekCountedFromTheStartWeek()
    {
        // Starting on a Wednesday: the start week (Mon 28 Sep) is "on", so its Monday is
        // before the start and excluded, but its Friday counts.
        var schedule = RecurrenceSchedule.Create(
            RecurrenceFrequency.BiWeekly, [DayOfWeek.Monday, DayOfWeek.Friday], D(2026, 9, 30), null);

        var occurrences = schedule.OccurrencesBetween(D(2026, 9, 1), D(2026, 10, 31));

        Assert.That(occurrences, Is.EqualTo(new[]
        {
            D(2026, 10, 2), D(2026, 10, 12), D(2026, 10, 16), D(2026, 10, 26), D(2026, 10, 30),
        }));
    }

    [Test]
    public void Monthly_RepeatsOnTheStartDayOfMonth()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, [], D(2026, 1, 15), null);

        var occurrences = schedule.OccurrencesBetween(D(2026, 1, 1), D(2026, 4, 30));

        Assert.That(occurrences, Is.EqualTo(new[] { D(2026, 1, 15), D(2026, 2, 15), D(2026, 3, 15), D(2026, 4, 15) }));
    }

    [Test]
    public void Monthly_ClampsToMonthEndWithoutDrifting()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, [], D(2026, 1, 31), null);

        var occurrences = schedule.OccurrencesBetween(D(2026, 1, 1), D(2026, 5, 31));

        Assert.That(occurrences, Is.EqualTo(new[]
        {
            D(2026, 1, 31), D(2026, 2, 28), D(2026, 3, 31), D(2026, 4, 30), D(2026, 5, 31),
        }));
    }

    [Test]
    public void Quarterly_RepeatsEveryThreeMonths()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Quarterly, [], D(2026, 2, 10), null);

        var occurrences = schedule.OccurrencesBetween(D(2026, 1, 1), D(2026, 12, 31));

        Assert.That(occurrences, Is.EqualTo(new[] { D(2026, 2, 10), D(2026, 5, 10), D(2026, 8, 10), D(2026, 11, 10) }));
    }

    [Test]
    public void Yearly_FromLeapDay_FallsOnFebruary28thInOtherYears()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Yearly, [], D(2028, 2, 29), null);

        var occurrences = schedule.OccurrencesBetween(D(2028, 1, 1), D(2032, 12, 31));

        Assert.That(occurrences, Is.EqualTo(new[]
        {
            D(2028, 2, 29), D(2029, 2, 28), D(2030, 2, 28), D(2031, 2, 28), D(2032, 2, 29),
        }));
    }

    [Test]
    public void OccurrencesBetween_StopsAtTheEndDateInclusive()
    {
        var schedule = RecurrenceSchedule.Create(
            RecurrenceFrequency.Weekly, [DayOfWeek.Monday], Monday, Monday.AddDays(7));

        var occurrences = schedule.OccurrencesBetween(Monday, Monday.AddDays(30));

        Assert.That(occurrences, Is.EqualTo(new[] { Monday, Monday.AddDays(7) }));
    }

    [Test]
    public void OccurrencesBetween_NeverReturnsDatesBeforeTheStart()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, [], D(2026, 6, 1), null);

        Assert.That(schedule.OccurrencesBetween(D(2026, 1, 1), D(2026, 5, 31)), Is.Empty);
    }

    [Test]
    public void NextOccurrenceOnOrAfter_IncludesTheDateItself()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, [DayOfWeek.Monday], Monday, null);

        Assert.That(schedule.NextOccurrenceOnOrAfter(Monday), Is.EqualTo(Monday));
        Assert.That(schedule.NextOccurrenceOnOrAfter(Monday.AddDays(1)), Is.EqualTo(Monday.AddDays(7)));
    }

    [Test]
    public void NextOccurrenceOnOrAfter_FindsAYearlyOccurrenceAlmostAYearAway()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Yearly, [], D(2026, 1, 1), null);

        Assert.That(schedule.NextOccurrenceOnOrAfter(D(2026, 1, 2)), Is.EqualTo(D(2027, 1, 1)));
    }

    [Test]
    public void NextOccurrenceOnOrAfter_AfterTheEnd_IsNull()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, [], D(2026, 1, 1), D(2026, 3, 1));

        Assert.That(schedule.NextOccurrenceOnOrAfter(D(2026, 3, 2)), Is.Null);
    }
}
