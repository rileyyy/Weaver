using Weaver.Application.Services;

namespace Weaver.Api.IntegrationTests;

/// <summary>For a service built outside a request that only reads, and so never needs today's date.</summary>
public class NoCalendar : IServerCalendar
{
    public Task<DateOnly> TodayAsync(CancellationToken ct = default) =>
        throw new InvalidOperationException("A read-only test has no calendar.");
}
