using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class DependencyConflictTools(IDependencyConflicts conflicts)
{
    private readonly IDependencyConflicts _conflicts = conflicts;

    [McpServerTool(Name = "list_dependency_conflicts", ReadOnly = true)]
    [Description("Lists dependency conflicts: work items scheduled to start before an item that blocks them " +
        "ends. Each names the blocked item and its blocker (id, number, title, Space and dates), earliest blocked " +
        "start first. Starting on the blocker's end date is not a conflict, nor is anything without the dates to " +
        "compare, a blocker in the Trash, or a link where either item is Done. A conflict usually means someone " +
        "forgot to update a date, and which date is wrong is a person's call: raise each conflict with a human " +
        "(for example in a comment) rather than rescheduling either item yourself.")]
    public Task<IReadOnlyList<DependencyConflictDto>> ListDependencyConflicts(
        [Description("Only list conflicts with the blocked item or its blocker in this Space. Use list_spaces to find valid ids. Omit for every Space.")] Guid? spaceId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var found = await _conflicts.ListAsync(spaceId, ct);
            return (IReadOnlyList<DependencyConflictDto>)found.Select(DependencyConflictDto.FromConflict).ToList();
        });
}
