namespace Weaver.Application.Services;

public class ServerCalendar : IServerCalendar
{
    private readonly IServerSettingsService _settings;
    private readonly TimeProvider _clock;

    public ServerCalendar(IServerSettingsService settings, TimeProvider clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public async Task<DateOnly> TodayAsync(CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(ct);
        return settings.DateAt(_clock.GetUtcNow());
    }
}
