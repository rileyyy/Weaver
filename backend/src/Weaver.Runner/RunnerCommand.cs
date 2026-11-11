using Weaver.Runner.Tasks;

namespace Weaver.Runner;

/// <summary>What the command line asked for. <see cref="Kind"/> is set for <see cref="RunnerMode.RunOnce"/>, <see cref="Error"/> for <see cref="RunnerMode.Invalid"/>.</summary>
public sealed record RunnerCommand(RunnerMode Mode, TaskKind? Kind = null, string? Error = null);
