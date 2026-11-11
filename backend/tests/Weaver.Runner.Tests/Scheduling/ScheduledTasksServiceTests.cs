using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Weaver.Runner.Configuration;
using Weaver.Runner.Model;
using Weaver.Runner.Scheduling;
using Weaver.Runner.Tasks;
using Weaver.Runner.Tests.Fakes;

namespace Weaver.Runner.Tests.Scheduling;

/// <summary>
/// The service hands its result to the process through <see cref="Environment.ExitCode"/>, so
/// these tests restore it and mustn't run alongside anything else that reads it.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ScheduledTasksServiceTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private FakeTimeProvider _time = null!;
    private FakeTaskRunFactory _runs = null!;
    private FakeHostLifetime _lifetime = null!;
    private int _exitCodeBefore;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 11, 2, 59, 0, TimeSpan.Zero));
        _runs = new FakeTaskRunFactory();
        _lifetime = new FakeHostLifetime();
        _exitCodeBefore = Environment.ExitCode;
    }

    [TearDown]
    public void TearDown()
    {
        Environment.ExitCode = _exitCodeBefore;
        _runs.Started.Dispose();
    }

    [Test]
    public async Task AStoppedRun_StopsTheApplication_WithTheRunsExitCode()
    {
        _runs.Run = _ => throw new ModelUnavailableException("connection refused");
        using var service = NewService(TestConfigs.WhenResolution("0 3 * * *"));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _lifetime.StopRequested.WaitAsync(Patience);

        Assert.That(Environment.ExitCode, Is.EqualTo(ExitCodes.RunStopped));
    }

    [Test]
    public async Task OnlyScheduledTaskKinds_AreHandedToTheLoop()
    {
        using var service = NewService(TestConfigs.WhenResolution(cron: null));

        await service.StartAsync(CancellationToken.None);
        await _lifetime.StopRequested.WaitAsync(Patience);

        Assert.Multiple(() =>
        {
            Assert.That(_runs.Starts, Is.Zero);
            Assert.That(Environment.ExitCode, Is.EqualTo(ExitCodes.Success));
        });
    }

    private ScheduledTasksService NewService(params TaskConfig[] kinds)
    {
        var executor = new TaskRunExecutor(_runs, NullLogger<TaskRunExecutor>.Instance);
        var loop = new ScheduleLoop(executor, _time, NullLogger<ScheduleLoop>.Instance);
        return new ScheduledTasksService(loop, new RunnerConfig(kinds), _lifetime);
    }
}
