using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weaver.Runner.Configuration;
using Weaver.Runner.Mentions;
using Weaver.Runner.Model;
using Weaver.Runner.WeaverApi;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tasks;

/// <summary>
/// Wires each task kind to its own model endpoint and Weaver agent key. The HTTP clients are
/// kept for the life of the process, one pair per task kind.
/// </summary>
public sealed class TaskRunFactory : ITaskRunFactory, IDisposable
{
    /// <summary>
    /// HttpClient's own default. Unlike the model's timeout it isn't configurable: the runner's
    /// requests to Weaver are small, so hitting it means Weaver is in trouble rather than slow.
    /// </summary>
    public static readonly TimeSpan WeaverTimeout = TimeSpan.FromSeconds(100);

    /// <summary>Recycles connections so a long-running runner notices a model or Weaver moving to a new address.</summary>
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Each run gets a new Weaver client over the kind's shared HTTP client, so each run has its own run id.</summary>
    private static readonly IReadOnlyDictionary<TaskKind, Func<HttpClient, string, IModelClient, ILoggerFactory, ITaskRun>> Runs =
        new Dictionary<TaskKind, Func<HttpClient, string, IModelClient, ILoggerFactory, ITaskRun>>
        {
            [TaskKind.WhenResolution] = (weaverHttp, agentApiKey, model, loggers) =>
                new WhenResolutionRun(new HttpWeaverClient(weaverHttp, agentApiKey), model, loggers.CreateLogger<WhenResolutionRun>()),
            [TaskKind.Mentions] = (weaverHttp, agentApiKey, model, loggers) =>
                new MentionsRun(new HttpMentionsWeaverClient(weaverHttp, agentApiKey), model, loggers.CreateLogger<MentionsRun>()),
        };

    private readonly ILoggerFactory _loggers;
    private readonly Func<Uri, TimeSpan, HttpClient> _newClient;
    private readonly ConcurrentDictionary<TaskKind, (HttpClient Weaver, HttpClient Model)> _clients = new();

    public TaskRunFactory(ILoggerFactory loggers)
        : this(loggers, NewPooledClient)
    {
    }

    internal TaskRunFactory(ILoggerFactory loggers, Func<Uri, TimeSpan, HttpClient> newClient)
    {
        _loggers = loggers;
        _newClient = newClient;
    }

    public ITaskRun Create(TaskConfig config)
    {
        if (!Runs.TryGetValue(config.Kind, out var newRun))
        {
            throw new NotSupportedException($"No run is defined for task kind {config.Kind}");
        }

        var (weaverHttp, modelHttp) = _clients.GetOrAdd(config.Kind, _ => (
            _newClient(config.WeaverUrl, WeaverTimeout),
            _newClient(config.ModelEndpoint, config.ModelTimeout)));
        var model = new OpenAiCompatibleModelClient(modelHttp, config.Model, config.ModelApiKey, config.JsonMode);
        return newRun(weaverHttp, config.AgentApiKey, model, _loggers);
    }

    public void Dispose()
    {
        foreach (var (weaver, model) in _clients.Values)
        {
            weaver.Dispose();
            model.Dispose();
        }
    }

    private static HttpClient NewPooledClient(Uri baseAddress, TimeSpan timeout) =>
        new(new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime })
        {
            BaseAddress = baseAddress,
            Timeout = timeout,
        };
}
