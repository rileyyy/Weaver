using Microsoft.Extensions.Logging;
using Weaver.Runner.Configuration;
using Weaver.Runner.Tasks;

namespace Weaver.Runner.Scheduling;

/// <summary>
/// Starts each scheduled task kind at its cron times until stopped. A run that has to stop ends
/// the whole loop with its exit code (ticket 46), so the failure shows as the container exiting
/// rather than only as a log line; a restart policy brings it back for the next scheduled time.
/// </summary>
public sealed class ScheduleLoop(TaskRunExecutor executor, TimeProvider time, ILogger<ScheduleLoop> logger)
{
    /// <summary>Kept well under <see cref="Task.Delay(TimeSpan)"/>'s limit of about 49 days.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromDays(1);

    private readonly TaskRunExecutor _executor = executor;
    private readonly TimeProvider _time = time;
    private readonly ILogger<ScheduleLoop> _logger = logger;

    public async Task<int> RunAsync(IReadOnlyList<TaskConfig> tasks, CancellationToken stoppingToken)
    {
        using var stopAll = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var loops = tasks.Select(t => RunTaskAsync(t, stopAll.Token)).ToList();
        while (loops.Count > 0)
        {
            var finished = await Task.WhenAny(loops);
            loops.Remove(finished);
            var exitCode = await finished;
            if (exitCode != ExitCodes.Success)
            {
                await stopAll.CancelAsync();
                await Task.WhenAll(loops);
                return exitCode;
            }
        }

        return ExitCodes.Success;
    }

    private async Task<int> RunTaskAsync(TaskConfig task, CancellationToken ct)
    {
        var schedule = task.Schedule!;
        try
        {
            while (schedule.Next(_time.GetUtcNow()) is { } next)
            {
                _logger.LogInformation("{Kind}: next run at {Next:yyyy-MM-dd HH:mm} ({TimeZone})",
                    task.Kind, TimeZoneInfo.ConvertTime(next, schedule.TimeZone), schedule.TimeZone.Id);
                await WaitUntilAsync(next, ct);
                var exitCode = await _executor.RunAsync(task, ct);
                if (exitCode != ExitCodes.Success)
                {
                    return exitCode;
                }
            }

            _logger.LogWarning("{Kind}: its cron expression has no further times", task.Kind);
            return ExitCodes.Success;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return ExitCodes.Success;
        }
    }

    private async Task WaitUntilAsync(DateTimeOffset when, CancellationToken ct)
    {
        for (var left = when - _time.GetUtcNow(); left > TimeSpan.Zero; left = when - _time.GetUtcNow())
        {
            await Task.Delay(left < LongestWait ? left : LongestWait, _time, ct);
        }
    }
}
