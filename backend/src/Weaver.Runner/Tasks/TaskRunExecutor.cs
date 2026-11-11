using Microsoft.Extensions.Logging;
using Weaver.Runner.Configuration;
using Weaver.Runner.Model;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tasks;

/// <summary>
/// Runs one task kind once and turns the outcome into an exit code: a run that had to stop
/// (model or Weaver unreachable) is a failure, while items skipped along the way are not.
/// </summary>
public sealed class TaskRunExecutor(ITaskRunFactory runs, ILogger<TaskRunExecutor> logger)
{
    private readonly ITaskRunFactory _runs = runs;
    private readonly ILogger<TaskRunExecutor> _logger = logger;

    public async Task<int> RunAsync(TaskConfig config, CancellationToken ct)
    {
        _logger.LogInformation("{Kind}: run starting with model {Model}", config.Kind, config.Model);
        try
        {
            var summary = await _runs.Create(config).RunAsync(ct);
            _logger.LogInformation(
                "{Kind}: run finished. {Considered} considered, {Written} written, {Skipped} skipped, {Rejected} answers rejected, {WriteFailed} writes refused, {Failed} failed",
                config.Kind, summary.Considered, summary.Written, summary.Skipped, summary.Rejected, summary.WriteFailed, summary.Failed);
            return ExitCodes.Success;
        }
        catch (Exception e) when (e is ModelUnavailableException or WeaverUnavailableException)
        {
            _logger.LogError("{Kind}: run stopped. {Reason}", config.Kind, e.Message);
            return ExitCodes.RunStopped;
        }
    }
}
