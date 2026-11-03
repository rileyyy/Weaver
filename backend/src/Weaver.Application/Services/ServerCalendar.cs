namespace Weaver.Application.Services;

public class ServerCalendar(IServerSettingsService settings, TimeProvider clock) : IServerCalendar
{
    private readonly IServerSettingsService _settings = settings;
    private readonly TimeProvider _clock = clock;

    public async Task<DateOnly> TodayAsync(CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(ct);
        return settings.DateAt(_clock.GetUtcNow());
    }
}
