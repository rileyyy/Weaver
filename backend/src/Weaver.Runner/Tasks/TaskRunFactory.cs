using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weaver.Runner.Configuration;
using Weaver.Runner.Model;
using Weaver.Runner.WeaverApi;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tasks;

/// <summary>
/// Wires each task kind to its own model endpoint and Weaver agent key. The HTTP clients are
/// kept for the life of the process, one pair per task kind.
/// </summary>
public sealed class TaskRunFactory(ILoggerFactory loggers) : ITaskRunFactory, IDisposable
{
    /// <summary>Recycles connections so a long-running runner notices a model or Weaver moving to a new address.</summary>
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(15);

    private readonly ILoggerFactory _loggers = loggers;
    private readonly ConcurrentDictionary<TaskKind, (HttpClient Weaver, HttpClient Model)> _clients = new();

    public ITaskRun Create(TaskConfig config)
    {
        var (weaverHttp, modelHttp) = _clients.GetOrAdd(config.Kind, _ => (
            NewClient(config.WeaverUrl, TimeSpan.FromSeconds(100)),
            NewClient(config.ModelEndpoint, config.ModelTimeout)));
        var weaver = new HttpWeaverClient(weaverHttp, config.AgentApiKey);
        var model = new OpenAiCompatibleModelClient(modelHttp, config.Model, config.ModelApiKey, config.JsonMode);

        return config.Kind == TaskKind.WhenResolution
            ? new WhenResolutionRun(weaver, model, _loggers.CreateLogger<WhenResolutionRun>())
            : throw new NotSupportedException($"No run is defined for task kind {config.Kind}");
    }

    public void Dispose()
    {
        foreach (var (weaver, model) in _clients.Values)
        {
            weaver.Dispose();
            model.Dispose();
        }
    }

    private static HttpClient NewClient(Uri baseAddress, TimeSpan timeout) =>
        new(new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime })
        {
            BaseAddress = baseAddress,
            Timeout = timeout,
        };
}
