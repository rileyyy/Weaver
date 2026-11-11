using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Runner.Tasks;
using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tests.Tasks;

[TestFixture]
public class TaskRunFactoryTests
{
    private readonly List<(Uri BaseAddress, TimeSpan Timeout, StubHttpHandler Handler)> _clientsMade = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var made in _clientsMade)
        {
            made.Handler.Dispose();
        }
    }

    [Test]
    public void WhenResolution_GetsAWhenResolutionRun()
    {
        using var factory = NewFactory();

        Assert.That(factory.Create(TestConfigs.WhenResolution()), Is.InstanceOf<WhenResolutionRun>());
    }

    [Test]
    public void AKindWithNoRunDefined_IsNotSupported()
    {
        using var factory = NewFactory();
        var config = TestConfigs.WhenResolution() with { Kind = new TaskKind("not-built", "NotBuilt") };

        Assert.Throws<NotSupportedException>(() => factory.Create(config));
    }

    [Test]
    public void AKindsHttpClients_AreMadeOnce_AndReusedByLaterRuns()
    {
        using var factory = NewFactory();
        var config = TestConfigs.WhenResolution();

        factory.Create(config);
        factory.Create(config);

        Assert.That(_clientsMade.Select(c => (c.BaseAddress, c.Timeout)), Is.EquivalentTo(new[]
        {
            (config.WeaverUrl, TaskRunFactory.WeaverTimeout),
            (config.ModelEndpoint, config.ModelTimeout),
        }));
    }

    [Test]
    public void Disposing_DisposesTheHttpClients()
    {
        var factory = NewFactory();
        factory.Create(TestConfigs.WhenResolution());

        factory.Dispose();

        Assert.That(_clientsMade.Select(c => c.Handler.Disposed), Is.All.True);
    }

    private TaskRunFactory NewFactory() => new(NullLoggerFactory.Instance, (baseAddress, timeout) =>
    {
        var handler = new StubHttpHandler();
        _clientsMade.Add((baseAddress, timeout, handler));
        return new HttpClient(handler) { BaseAddress = baseAddress, Timeout = timeout };
    });
}
