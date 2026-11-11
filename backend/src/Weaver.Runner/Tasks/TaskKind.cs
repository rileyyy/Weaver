namespace Weaver.Runner.Tasks;

/// <summary>
/// One job the runner can do. <see cref="CommandName"/> is what <c>--run</c> takes and what actor
/// labels name; <see cref="ConfigKey"/> is its section under <c>Tasks</c>, kept free of hyphens so
/// it can be set from environment variables (<c>Tasks__WhenResolution__Model</c>).
/// </summary>
public sealed record TaskKind(string CommandName, string ConfigKey)
{
    public static readonly TaskKind WhenResolution = new("when-resolution", "WhenResolution");

    /// <summary>Answers the agent account's pending <c>@</c> mentions in comments (ticket 51).</summary>
    public static readonly TaskKind Mentions = new("mentions", "Mentions");

    public static IReadOnlyList<TaskKind> All { get; } = [WhenResolution, Mentions];

    public static TaskKind? FromCommandName(string name) =>
        All.FirstOrDefault(k => string.Equals(k.CommandName, name, StringComparison.OrdinalIgnoreCase));

    public static TaskKind? FromConfigKey(string key) =>
        All.FirstOrDefault(k => string.Equals(k.ConfigKey, key, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => CommandName;
}
