using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Weaver.Api.Background;
using Weaver.Application.Services;

namespace Weaver.Api.Tests.Background;

[TestFixture]
public class TrashPurgeWorkerTests
{
    private static TrashPurgeWorker CreateWorker(IWorkItemPurgeService service)
    {
        var provider = new ServiceCollection().AddScoped(_ => service).BuildServiceProvider();
        return new TrashPurgeWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TrashPurgeWorker>.Instance);
    }

    [Test]
    public async Task Start_PurgesExpiredDeletesImmediately()
    {
        var purged = new TaskCompletionSource();
        var service = new Mock<IWorkItemPurgeService>(MockBehavior.Strict);
        service.Setup(s => s.PurgeExpiredAsync(It.IsAny<CancellationToken>()))
            .Callback(purged.SetResult)
            .ReturnsAsync(0);
        using var worker = CreateWorker(service.Object);

        await worker.StartAsync(CancellationToken.None);
        await purged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        service.Verify(s => s.PurgeExpiredAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AFailedRun_DoesNotStopTheWorker()
    {
        var attempted = new TaskCompletionSource();
        var service = new Mock<IWorkItemPurgeService>(MockBehavior.Strict);
        service.Setup(s => s.PurgeExpiredAsync(It.IsAny<CancellationToken>()))
            .Callback(attempted.SetResult)
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        using var worker = CreateWorker(service.Object);

        await worker.StartAsync(CancellationToken.None);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Give the exception time to escape ExecuteAsync if it were going to.
        await Task.Delay(100);

        Assert.That(worker.ExecuteTask!.IsCompleted, Is.False);
        await worker.StopAsync(CancellationToken.None);
    }
}
