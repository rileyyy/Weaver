using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Runner.Model;
using Weaver.Runner.Tasks;
using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.Tasks;

[TestFixture]
public class TaskRunExecutorTests
{
    private FakeTaskRunFactory _runs = null!;

    [SetUp]
    public void SetUp() => _runs = new FakeTaskRunFactory();

    [TearDown]
    public void TearDown() => _runs.Started.Dispose();

    [Test]
    public async Task ACompletedRun_Succeeds_EvenWithSkippedItems()
    {
        _runs.Run = _ => Task.FromResult(new RunSummary(Considered: 3, Written: 1, Skipped: 1, Rejected: 1, WriteFailed: 0));

        Assert.That(await RunAsync(), Is.EqualTo(ExitCodes.Success));
    }

    [Test]
    public async Task AnUnreachableModel_StopsTheRun_WithANonZeroExit()
    {
        _runs.Run = _ => throw new ModelUnavailableException("connection refused");

        Assert.That(await RunAsync(), Is.EqualTo(ExitCodes.RunStopped));
    }

    [Test]
    public async Task AnUnreachableWeaver_StopsTheRun_WithANonZeroExit()
    {
        _runs.Run = _ => throw new WeaverUnavailableException("connection refused");

        Assert.That(await RunAsync(), Is.EqualTo(ExitCodes.RunStopped));
    }

    private Task<int> RunAsync() =>
        new TaskRunExecutor(_runs, NullLogger<TaskRunExecutor>.Instance).RunAsync(TestConfigs.WhenResolution(), CancellationToken.None);
}
