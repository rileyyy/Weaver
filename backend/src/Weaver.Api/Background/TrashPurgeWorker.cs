using Weaver.Application.Services;

namespace Weaver.Api.Background;

/// <summary>
/// Purges deletes that have outlived the Trash retention period (ADR-0007), at startup and
/// then hourly, so nothing stays in the Trash much more than an hour past its expiry, even
/// across restarts. A tick with nothing expired costs one cheap query.
/// </summary>
public class TrashPurgeWorker(IServiceScopeFactory scopes, ILogger<TrashPurgeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly ILogger<TrashPurgeWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await PurgeExpiredAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeExpiredAsync(CancellationToken ct)
    {
        // An exception escaping ExecuteAsync stops the whole host, so a database outage must
        // only cost this tick.
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var purge = scope.ServiceProvider.GetRequiredService<IWorkItemPurgeService>();
            var purged = await purge.PurgeExpiredAsync(ct);
            if (purged > 0)
            {
                _logger.LogInformation("Purged {Count} expired work items from the Trash", purged);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Purging expired work items from the Trash failed");
        }
    }
}
