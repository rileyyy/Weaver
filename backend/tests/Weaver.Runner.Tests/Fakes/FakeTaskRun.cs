using Weaver.Runner.Tasks;

namespace Weaver.Runner.Tests.Fakes;

internal sealed class FakeTaskRun(Func<CancellationToken, Task<RunSummary>> run) : ITaskRun
{
    public Task<RunSummary> RunAsync(CancellationToken ct) => run(ct);
}
