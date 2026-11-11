using Weaver.Runner.Tasks;

namespace Weaver.Runner.Configuration;

/// <summary>The task kinds this runner is configured for. A kind left out of the config isn't run.</summary>
public sealed record RunnerConfig(IReadOnlyList<TaskConfig> Tasks)
{
    public TaskConfig? For(TaskKind kind) => Tasks.FirstOrDefault(t => t.Kind == kind);
}
