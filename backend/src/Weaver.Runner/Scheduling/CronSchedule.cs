using Cronos;

namespace Weaver.Runner.Scheduling;

/// <summary>
/// A task kind's schedule: a standard five-field cron expression read in a time zone, so
/// "0 3 * * *" means 3am where the runner's owner lives, across daylight-saving changes.
/// </summary>
public sealed class CronSchedule
{
    private readonly CronExpression _expression;

    private CronSchedule(CronExpression expression, TimeZoneInfo timeZone)
    {
        _expression = expression;
        TimeZone = timeZone;
    }

    public TimeZoneInfo TimeZone { get; }

    public static CronSchedule Parse(string expression, TimeZoneInfo timeZone) =>
        TryParse(expression, timeZone, out var schedule)
            ? schedule
            : throw new FormatException($"Not a cron expression: {expression}");

    public static bool TryParse(string expression, TimeZoneInfo timeZone, out CronSchedule schedule)
    {
        schedule = null!;
        if (!CronExpression.TryParse(expression, CronFormat.Standard, out var parsed))
        {
            return false;
        }

        schedule = new CronSchedule(parsed, timeZone);
        return true;
    }

    /// <summary>The first occurrence strictly after <paramref name="now"/>, or null if there is none.</summary>
    public DateTimeOffset? Next(DateTimeOffset now) => _expression.GetNextOccurrence(now, TimeZone);
}
