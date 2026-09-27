using Weaver.Application.Services;

namespace Weaver.Api.Background;

/// <summary>
/// Creates due occurrences of repeating work items at startup and then hourly. Hourly rather
/// than daily so a restart or a missed tick never delays an occurrence by more than an hour;
/// generation is idempotent, so running often costs one cheap query when nothing is due.
/// </summary>
public class RecurrenceGenerationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecurrenceGenerationWorker> _logger;

    public RecurrenceGenerationWorker(IServiceScopeFactory scopes, ILogger<RecurrenceGenerationWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await GenerateAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task GenerateAsync(CancellationToken ct)
    {
        // An exception escaping ExecuteAsync stops the whole host, so a database outage must
        // only cost this tick.
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var recurrences = scope.ServiceProvider.GetRequiredService<IWorkItemRecurrenceService>();
            var created = await recurrences.GenerateDueAsync(ct);
            if (created > 0)
            {
                _logger.LogInformation("Created {Count} repeating work item occurrences", created);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Generating repeating work item occurrences failed");
        }
    }
}
