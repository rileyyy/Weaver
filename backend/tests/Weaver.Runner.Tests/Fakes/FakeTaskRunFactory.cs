using Weaver.Runner.Configuration;
using Weaver.Runner.Tasks;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>Every run it makes calls <see cref="Run"/>, and each start is counted and signalled.</summary>
internal sealed class FakeTaskRunFactory : ITaskRunFactory
{
    public Func<CancellationToken, Task<RunSummary>> Run { get; set; } =
        _ => Task.FromResult(new RunSummary(0, 0, 0, 0, 0));

    public int Starts { get; private set; }

    public SemaphoreSlim Started { get; } = new(0);

    public ITaskRun Create(TaskConfig config) => new FakeTaskRun(StartAsync);

    private Task<RunSummary> StartAsync(CancellationToken ct)
    {
        Starts++;
        Started.Release();
        return Run(ct);
    }
}
