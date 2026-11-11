namespace Weaver.Runner.Tasks;

/// <summary>
/// One job the runner can do. <see cref="CommandName"/> is what <c>--run</c> takes and what actor
/// labels name; <see cref="ConfigKey"/> is its section under <c>Tasks</c>, kept free of hyphens so
/// it can be set from environment variables (<c>Tasks__WhenResolution__Model</c>).
/// </summary>
public sealed record TaskKind(string CommandName, string ConfigKey)
{
    public static readonly TaskKind WhenResolution = new("when-resolution", "WhenResolution");

    public static IReadOnlyList<TaskKind> All { get; } = [WhenResolution];

    public static TaskKind? FromCommandName(string name) =>
        All.FirstOrDefault(k => string.Equals(k.CommandName, name, StringComparison.OrdinalIgnoreCase));

    public static TaskKind? FromConfigKey(string key) =>
        All.FirstOrDefault(k => string.Equals(k.ConfigKey, key, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => CommandName;
}
