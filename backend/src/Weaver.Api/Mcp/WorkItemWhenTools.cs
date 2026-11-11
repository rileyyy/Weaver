using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

/// <summary>
/// How agents turn When phrases into dates and links (ADR-0003). Weaver has no LLM, so reading a
/// phrase is the agent's job; resolving it goes through the ordinary schedule and link writes.
/// </summary>
[McpServerToolType]
public class WorkItemWhenTools(IWorkItemQueryService query)
{
    private readonly IWorkItemQueryService _query = query;

    [McpServerTool(Name = "list_unresolved_when_items", ReadOnly = true)]
    [Description("Lists work items whose When is unresolved: they have a When phrase (e.g. \"after drywall\", " +
        "\"next spring\") but no start or end date and no live item blocking them. Each comes as a brief " +
        "summary with its when phrase, Space and parentId; Done items are included unless statusCategories " +
        "leaves them out. Resolve a phrase that names a time with reschedule_work_item, and one that names " +
        "other work with create_work_item_link (type BlockedBy, targetWorkItemId the work it waits for); the " +
        "phrase is kept and the item drops off this list. Before linking, find the referenced item with " +
        "search_work_items (start with the same Space) and link only when exactly one item clearly matches. " +
        "If a phrase is ambiguous, or could mean several items or dates, leave it unresolved for a human to " +
        "decide rather than guessing. Items come in Number order; while a page has a nextCursor, call again " +
        "with the same arguments and that cursor for the next page.")]
    public Task<WorkItemSummaryPageDto> ListUnresolvedWhenItems(
        [Description("Only items in these Spaces. Use list_spaces to find valid ids. Omit for every Space.")] IReadOnlyList<Guid>? spaceIds = null,
        [Description("Only items whose status is in one of these categories: ToDo, Doing, Done. Omit for all of them.")] IReadOnlyList<StatusCategory>? statusCategories = null,
        [Description("The nextCursor from the previous page. Omit for the first page.")] string? cursor = null,
        [Description("Items per page, 1 to 1000. Defaults to 50.")] int limit = WorkItemQuery.DefaultLimit,
        [Description("Also list items whose When a human edited after their dates were set: a request to " +
            "resolve it again. They keep their old dates and come with whenEditedSinceResolved true. Replace " +
            "their dates with reschedule_work_item; never remove their links.")] bool includeEdited = false,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var page = await _query.QuerySummariesAsync(
                new WorkItemQuery(
                    SpaceIds: spaceIds,
                    StatusCategories: statusCategories,
                    UnresolvedWhenOnly: true,
                    IncludeEditedWhen: includeEdited,
                    Cursor: cursor,
                    Limit: limit),
                ct);
            return WorkItemSummaryPageDto.FromPage(page);
        });
}
