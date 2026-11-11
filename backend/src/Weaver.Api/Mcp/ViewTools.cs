using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

/// <summary>
/// Agents list and run Views but don't save, change or delete them: Views are the humans'
/// shared layouts, and an agent can ask anything a View asks through search_work_items.
/// </summary>
[McpServerToolType]
public class ViewTools(IViewService views)
{
    private readonly IViewService _views = views;

    [McpServerTool(Name = "list_views", ReadOnly = true)]
    [Description("Lists the saved Views people use to look at work, Overview first. A View is a stored " +
        "search: its Spaces (empty means every Space), text, tags, tagMatch, applyDoneHiding and " +
        "scheduledOrInProgressOnly, plus the layout people see it in. Overview is built in: everything in a " +
        "Doing status or with dates, across every Space. Use run_view to list a View's work items.")]
    public async Task<IReadOnlyList<ViewDto>> ListViews(CancellationToken ct)
    {
        var views = await _views.ListAsync(ct);
        return views.Select(ViewDto.FromEntity).ToList();
    }

    [McpServerTool(Name = "run_view", ReadOnly = true)]
    [Description("Lists the work items a View shows, each as a brief summary with names, in Number order. " +
        "A View stores no dates, so pass from and/or to for a window; with one, dated items must overlap it " +
        "and undated items only appear as the parent of one that does (Overview still lists every Doing " +
        "item). While a page has a nextCursor, call again with the same arguments and that cursor.")]
    public Task<WorkItemSummaryPageDto> RunView(
        [Description("The View's id, from list_views.")] Guid viewId,
        [Description("Start of a date window, inclusive (yyyy-MM-dd). Omit for no window.")] DateOnly? from = null,
        [Description("End of the date window, inclusive (yyyy-MM-dd).")] DateOnly? to = null,
        [Description("The nextCursor from the previous page. Omit for the first page.")] string? cursor = null,
        [Description("Items per page, 1 to 1000. Defaults to 50.")] int limit = WorkItemQuery.DefaultLimit,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
            WorkItemSummaryPageDto.FromPage(await _views.RunAsync(viewId, new ViewWindow(from, to, cursor, limit), ct)));
}
