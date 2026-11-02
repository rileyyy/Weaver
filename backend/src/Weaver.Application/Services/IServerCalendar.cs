namespace Weaver.Application.Services;

/// <summary>
/// "Today" as the server means it: the current date in the admin-set server time zone. Every
/// rule that compares calendar dates with the present should ask this rather than read the
/// clock, so they all agree on when a day starts.
/// </summary>
public interface IServerCalendar
{
    Task<DateOnly> TodayAsync(CancellationToken ct = default);
}
