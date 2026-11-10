using ModelContextProtocol;

namespace Weaver.Api.Mcp;

/// <summary>
/// Reads <c>get_work_item_context</c>'s <c>include</c> list. The values are plain strings rather
/// than an enum argument so that a wrong one is answered with the valid ones, instead of the
/// SDK's generic failure to bind the arguments.
/// </summary>
public static class WorkItemContextIncludes
{
    private static readonly IReadOnlyList<WorkItemContextInclude> Valid =
    [
        WorkItemContextInclude.Ancestors,
        WorkItemContextInclude.Children,
        WorkItemContextInclude.Comments,
        WorkItemContextInclude.Links,
        WorkItemContextInclude.History,
        WorkItemContextInclude.Attachments,
    ];

    private static readonly IReadOnlyDictionary<string, WorkItemContextInclude> ByName =
        Valid.ToDictionary(Name, StringComparer.OrdinalIgnoreCase);

    public static string ValidNames { get; } = string.Join(", ", Valid.Select(Name));

    public static WorkItemContextInclude Parse(IEnumerable<string>? values)
    {
        var requested = (values ?? []).Select(v => v.Trim()).ToList();
        var unknown = requested.Where(v => !ByName.ContainsKey(v)).Select(v => $"\"{v}\"").ToList();
        if (unknown.Count > 0)
        {
            throw new McpException($"Unknown include value(s): {string.Join(", ", unknown)}. Valid values: {ValidNames}.");
        }

        return requested.Aggregate(WorkItemContextInclude.None, (all, v) => all | ByName[v]);
    }

    private static string Name(WorkItemContextInclude include) => include.ToString().ToLowerInvariant();
}
