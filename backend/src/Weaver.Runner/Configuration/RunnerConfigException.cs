namespace Weaver.Runner.Configuration;

public sealed class RunnerConfigException(IReadOnlyList<string> problems)
    : Exception("The runner's configuration has problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "- " + p)))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
