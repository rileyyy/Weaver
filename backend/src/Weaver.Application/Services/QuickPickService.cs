using Weaver.Domain;

namespace Weaver.Application.Services;

public class QuickPickService : IQuickPickService
{
    private readonly IServerCalendar _calendar;

    public QuickPickService(IServerCalendar calendar)
    {
        _calendar = calendar;
    }

    public async Task<IReadOnlyList<ResolvedQuickPick>> ListAsync(CancellationToken ct = default)
    {
        var today = await _calendar.TodayAsync(ct);
        return Enum.GetValues<QuickPick>()
            .Select(pick => new ResolvedQuickPick(pick, QuickPickDates.Resolve(pick, today)))
            .ToList();
    }
}
