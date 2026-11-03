namespace Weaver.Domain;

/// <summary>
/// Turns a <see cref="QuickPick"/> into dates relative to <c>today</c>, which the caller must
/// take from the server calendar so picks agree with the server's time zone. Weeks run Monday
/// to Sunday. A range never starts in the past: on a Sunday "this weekend" is just today.
/// </summary>
public static class QuickPickDates
{
    public static DateRange Resolve(QuickPick pick, DateOnly today) => pick switch
    {
        QuickPick.ThisWeekend => ThisWeekend(today),
        QuickPick.NextWeek => NextWeek(today),
        QuickPick.NextMonth => NextMonth(today),
        _ => throw new ArgumentOutOfRangeException(nameof(pick), pick, null),
    };

    private static DateRange ThisWeekend(DateOnly today)
    {
        if (today.DayOfWeek == DayOfWeek.Sunday)
        {
            return new DateRange(today, today);
        }

        var saturday = today.AddDays(DayOfWeek.Saturday - today.DayOfWeek);
        return new DateRange(saturday, saturday.AddDays(1));
    }

    private static DateRange NextWeek(DateOnly today)
    {
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var nextMonday = today.AddDays(7 - daysSinceMonday);
        return new DateRange(nextMonday, nextMonday.AddDays(6));
    }

    private static DateRange NextMonth(DateOnly today)
    {
        var firstOfNextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        return new DateRange(firstOfNextMonth, firstOfNextMonth.AddMonths(1).AddDays(-1));
    }
}
