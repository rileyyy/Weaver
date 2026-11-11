using Microsoft.Extensions.Hosting;

namespace Weaver.Runner.Tests.Fakes;

internal sealed class FakeHostLifetime : IHostApplicationLifetime
{
    private readonly TaskCompletionSource _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public Task StopRequested => _stopRequested.Task;

    public void StopApplication() => _stopRequested.TrySetResult();
}
