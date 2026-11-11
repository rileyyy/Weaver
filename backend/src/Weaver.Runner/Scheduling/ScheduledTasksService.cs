using Microsoft.Extensions.Hosting;
using Weaver.Runner.Configuration;

namespace Weaver.Runner.Scheduling;

/// <summary>Hosts <see cref="ScheduleLoop"/> and ends the process with its exit code when it ends.</summary>
public sealed class ScheduledTasksService(ScheduleLoop loop, RunnerConfig config, IHostApplicationLifetime lifetime)
    : BackgroundService
{
    private readonly ScheduleLoop _loop = loop;
    private readonly RunnerConfig _config = config;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scheduled = _config.Tasks.Where(t => t.Schedule is not null).ToList();
        Environment.ExitCode = await _loop.RunAsync(scheduled, stoppingToken);
        _lifetime.StopApplication();
    }
}
