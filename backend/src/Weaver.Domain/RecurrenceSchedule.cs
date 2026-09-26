using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// When a repeating work item recurs. Weekly and bi-weekly schedules fall on the chosen
/// weekdays; monthly, quarterly and yearly ones fall on the start date's day of the month
/// (clamped to the month's last day) and ignore weekdays.
/// </summary>
public sealed record RecurrenceSchedule
{
    private RecurrenceSchedule(RecurrenceFrequency frequency, RecurrenceDays days, DateOnly startDate, DateOnly? endDate)
    {
        Frequency = frequency;
        Days = days;
        StartDate = startDate;
        EndDate = endDate;
    }

    public RecurrenceFrequency Frequency { get; }

    public RecurrenceDays Days { get; }

    public DateOnly StartDate { get; }

    /// <summary>Inclusive; null repeats indefinitely.</summary>
    public DateOnly? EndDate { get; }

    public bool IsWeekBased => IsWeekBasedFrequency(Frequency);

    /// <summary>
    /// Validates and normalises a schedule. Weekdays are dropped for month-based
    /// frequencies so the stored row never claims days that have no effect.
    /// </summary>
    public static RecurrenceSchedule Create(
        RecurrenceFrequency frequency,
        IEnumerable<DayOfWeek> daysOfWeek,
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (!Enum.IsDefined(frequency))
        {
            throw new DomainValidationException($"Unknown repeat frequency '{frequency}'.");
        }

        if (endDate is not null && endDate < startDate)
        {
            throw new DomainValidationException("The repeat end date can't be before its start date.");
        }

        var days = IsWeekBasedFrequency(frequency) ? ToDays(daysOfWeek) : RecurrenceDays.None;
        if (IsWeekBasedFrequency(frequency) && days == RecurrenceDays.None)
        {
            throw new DomainValidationException("Choose at least one day of the week to repeat on.");
        }

        return new RecurrenceSchedule(frequency, days, startDate, endDate);
    }

    /// <summary>Rehydrates an already-validated schedule from storage.</summary>
    public static RecurrenceSchedule FromStored(
        RecurrenceFrequency frequency,
        RecurrenceDays days,
        DateOnly startDate,
        DateOnly? endDate) => new(frequency, days, startDate, endDate);

    public IReadOnlyList<DayOfWeek> DaysOfWeek =>
        Enum.GetValues<DayOfWeek>().Where(d => Days.HasFlag(ToFlag(d))).OrderBy(MondayFirstIndex).ToList();

    /// <summary>Every occurrence in <paramref name="from"/>..<paramref name="to"/> (inclusive), ascending.</summary>
    public IEnumerable<DateOnly> OccurrencesBetween(DateOnly from, DateOnly to)
    {
        var lower = from > StartDate ? from : StartDate;
        var upper = EndDate is not null && EndDate < to ? EndDate.Value : to;
        if (lower > upper)
        {
            return [];
        }

        return IsWeekBased ? WeekBasedOccurrences(lower, upper) : MonthBasedOccurrences(lower, upper);
    }

    /// <summary>The first occurrence on or after <paramref name="date"/>, or null if the schedule has ended.</summary>
    public DateOnly? NextOccurrenceOnOrAfter(DateOnly date)
    {
        // A year always contains at least one occurrence of any schedule that hasn't ended.
        foreach (var occurrence in OccurrencesBetween(date, date.AddYears(1)))
        {
            return occurrence;
        }

        return null;
    }

    private IEnumerable<DateOnly> WeekBasedOccurrences(DateOnly lower, DateOnly upper)
    {
        var anchorWeek = StartOfWeek(StartDate);
        for (var date = lower; date <= upper; date = date.AddDays(1))
        {
            if (!Days.HasFlag(ToFlag(date.DayOfWeek)))
            {
                continue;
            }

            var weeksSinceStart = (StartOfWeek(date).DayNumber - anchorWeek.DayNumber) / 7;
            if (Frequency == RecurrenceFrequency.Weekly || weeksSinceStart % 2 == 0)
            {
                yield return date;
            }
        }
    }

    private IEnumerable<DateOnly> MonthBasedOccurrences(DateOnly lower, DateOnly upper)
    {
        var step = Frequency switch
        {
            RecurrenceFrequency.Monthly => 1,
            RecurrenceFrequency.Quarterly => 3,
            _ => 12,
        };

        // Always offset from StartDate itself: stepping from the previous occurrence would
        // let a clamped day (31st -> 30th) drift permanently.
        for (var index = 0; ; index++)
        {
            var date = StartDate.AddMonths(index * step);
            if (date > upper)
            {
                yield break;
            }

            if (date >= lower)
            {
                yield return date;
            }
        }
    }

    private static bool IsWeekBasedFrequency(RecurrenceFrequency frequency) =>
        frequency is RecurrenceFrequency.Weekly or RecurrenceFrequency.BiWeekly;

    private static RecurrenceDays ToDays(IEnumerable<DayOfWeek> daysOfWeek) =>
        daysOfWeek.Aggregate(RecurrenceDays.None, (days, day) => days | ToFlag(day));

    private static RecurrenceDays ToFlag(DayOfWeek day) => (RecurrenceDays)(1 << MondayFirstIndex(day));

    private static int MondayFirstIndex(DayOfWeek day) => ((int)day + 6) % 7;

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-MondayFirstIndex(date.DayOfWeek));
}
