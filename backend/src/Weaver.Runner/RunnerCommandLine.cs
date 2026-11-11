using Weaver.Runner.Tasks;

namespace Weaver.Runner;

public static class RunnerCommandLine
{
    public static readonly string Usage =
        $"""
        Usage:
          Weaver.Runner                 run every configured task kind on its cron schedule
          Weaver.Runner --run <kind>    run one task kind once, then exit
        Task kinds: {KnownKinds}
        Exit codes: {ExitCodes.Success} done, {ExitCodes.RunStopped} a run stopped (model or Weaver unreachable), {ExitCodes.BadSetup} bad arguments or config
        """;

    private static string KnownKinds => string.Join(", ", TaskKind.All.Select(k => k.CommandName));

    public static RunnerCommand Parse(IReadOnlyList<string> args) => args switch
    {
        [] => new RunnerCommand(RunnerMode.Schedule),
        ["--help" or "-h"] => new RunnerCommand(RunnerMode.Help),
        ["--run", var name] => RunOnce(name),
        _ => new RunnerCommand(RunnerMode.Invalid, Error: $"Unexpected arguments: {string.Join(' ', args)}"),
    };

    private static RunnerCommand RunOnce(string name) =>
        TaskKind.FromCommandName(name) is { } kind
            ? new RunnerCommand(RunnerMode.RunOnce, kind)
            : new RunnerCommand(RunnerMode.Invalid, Error: $"Unknown task kind \"{name}\"; known: {KnownKinds}");
}
