using Weaver.Application.Services;

namespace Weaver.Application.Tests;

/// <summary>A server "today" the test sets by hand, for services that need only the date.</summary>
public sealed class FixedServerCalendar : IServerCalendar
{
    public static readonly DateOnly DefaultToday = new(2026, 11, 10);

    public DateOnly Today { get; set; } = DefaultToday;

    public Task<DateOnly> TodayAsync(CancellationToken ct = default) => Task.FromResult(Today);
}
