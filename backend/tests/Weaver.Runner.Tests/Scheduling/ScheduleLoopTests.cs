using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Weaver.Runner.Model;
using Weaver.Runner.Scheduling;
using Weaver.Runner.Tasks;
using Weaver.Runner.Tests.Fakes;

namespace Weaver.Runner.Tests.Scheduling;

[TestFixture]
public class ScheduleLoopTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private FakeTimeProvider _time = null!;
    private FakeTaskRunFactory _runs = null!;
    private CancellationTokenSource _stop = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 11, 2, 59, 0, TimeSpan.Zero));
        _runs = new FakeTaskRunFactory();
        _stop = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown()
    {
        _stop.Dispose();
        _runs.Started.Dispose();
    }

    [Test]
    public async Task ATaskKind_RunsAtEachScheduledTime_AndNotBefore()
    {
        var loop = StartLoop("0 3 * * *");

        await AdvanceAsync(TimeSpan.FromSeconds(59));
        var startsBeforeTime = _runs.Starts;
        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.That(await _runs.Started.WaitAsync(Patience), Is.True);
        await AdvanceAsync(TimeSpan.FromDays(1));
        Assert.That(await _runs.Started.WaitAsync(Patience), Is.True);

        await _stop.CancelAsync();
        var exitCode = await loop.WaitAsync(Patience);

        Assert.Multiple(() =>
        {
            Assert.That(startsBeforeTime, Is.Zero);
            Assert.That(_runs.Starts, Is.EqualTo(2));
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Success));
        });
    }

    [Test]
    public async Task AStoppedRun_EndsTheLoop_WithANonZeroExit()
    {
        _runs.Run = _ => throw new ModelUnavailableException("connection refused");
        var loop = StartLoop("0 3 * * *");

        await AdvanceAsync(TimeSpan.FromMinutes(1));

        Assert.That(await loop.WaitAsync(Patience), Is.EqualTo(ExitCodes.RunStopped));
    }

    private Task<int> StartLoop(string cron)
    {
        var executor = new TaskRunExecutor(_runs, NullLogger<TaskRunExecutor>.Instance);
        var loop = new ScheduleLoop(executor, _time, NullLogger<ScheduleLoop>.Instance);
        return loop.RunAsync([TestConfigs.WhenResolution(cron)], _stop.Token);
    }

    /// <summary>
    /// Lets the loop reach its wait before moving the clock: advancing first would fire nothing,
    /// since no timer would exist yet.
    /// </summary>
    private async Task AdvanceAsync(TimeSpan by)
    {
        await Task.Delay(50);
        _time.Advance(by);
    }
}
