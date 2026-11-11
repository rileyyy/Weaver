namespace Weaver.Runner.Tasks;

/// <summary>
/// One pass of a task kind over its work. Throws <see cref="Model.ModelUnavailableException"/> or
/// <see cref="WeaverApi.WeaverUnavailableException"/> when the run has to stop; a problem with
/// one item is logged and counted in the <see cref="RunSummary"/> instead.
/// </summary>
public interface ITaskRun
{
    Task<RunSummary> RunAsync(CancellationToken ct);
}
