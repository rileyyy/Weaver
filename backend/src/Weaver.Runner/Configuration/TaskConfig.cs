using Weaver.Runner.Scheduling;
using Weaver.Runner.Tasks;

namespace Weaver.Runner.Configuration;

/// <summary>
/// One task kind's checked settings. Weaver stores none of this (ADR-0010): the agent key is
/// the only thing tying the runner to an account there. With no <see cref="Schedule"/> the task
/// kind runs only on demand.
/// </summary>
public sealed record TaskConfig(
    TaskKind Kind,
    Uri ModelEndpoint,
    string Model,
    string? ModelApiKey,
    Uri WeaverUrl,
    string AgentApiKey,
    CronSchedule? Schedule,
    bool JsonMode,
    TimeSpan ModelTimeout);
