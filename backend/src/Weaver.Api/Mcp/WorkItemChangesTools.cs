using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemChangesTools(IWorkItemChangesService changes)
{
    private readonly IWorkItemChangesService _changes = changes;

    [McpServerTool(Name = "list_changes_since", ReadOnly = true)]
    [Description("Lists the work items changed at or after a moment (e.g. \"what changed since yesterday?\"), " +
        "each as a brief summary with who changed what since then, oldest change first. Deleted items are " +
        "included with deleted: true. Items come in the order each first changed; while nextCursor is " +
        "not null, call again with the same arguments and that cursor for the next page.")]
    public Task<WorkItemChangesPageDto> ListChangesSince(
        [Description("The moment to list changes from, inclusive, as an ISO 8601 timestamp with offset (e.g. 2026-11-02T09:00:00Z).")] DateTimeOffset since,
        [Description("Only list items in this Space; a deleted item counts as in the Space it was last in. Use list_spaces to find valid ids. Omit for every Space.")] Guid? spaceId = null,
        [Description("True to leave out changes made by your own account, so you see only what others (and Weaver itself) did. Defaults to false.")] bool excludeOwnChanges = false,
        [Description("The nextCursor from the previous page. Omit for the first page.")] string? cursor = null,
        [Description("Items per page, 1 to 200. Defaults to 50.")] int limit = WorkItemChangesQuery.DefaultLimit,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var page = await _changes.ListSinceAsync(new WorkItemChangesQuery(since, spaceId, excludeOwnChanges, cursor, limit), ct);
            return WorkItemChangesPageDto.FromPage(page);
        });
}
