using Weaver.Domain;

namespace Weaver.Application.Services;

public class QuickPickService(IServerCalendar calendar) : IQuickPickService
{
    private readonly IServerCalendar _calendar = calendar;

    public async Task<IReadOnlyList<ResolvedQuickPick>> ListAsync(CancellationToken ct = default)
    {
        var today = await _calendar.TodayAsync(ct);
        return Enum.GetValues<QuickPick>()
            .Select(pick => new ResolvedQuickPick(pick, QuickPickDates.Resolve(pick, today)))
            .ToList();
    }
}
