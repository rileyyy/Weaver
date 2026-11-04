using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemSearchTools(IWorkItemQueryService query)
{
    private readonly IWorkItemQueryService _query = query;

    [McpServerTool(Name = "search_work_items", ReadOnly = true)]
    [Description("Finds work items, each as a brief summary with names. Every argument is optional and " +
        "the ones given AND together; a list argument matches any of its values. Use text \"#123\" (or " +
        "number) to look up a work item by its Number. Items come in Number order; while nextCursor is not " +
        "null, call again with the same arguments and that cursor for the next page. Use get_work_item " +
        "with a summary's id for full detail.")]
    public Task<WorkItemSummaryPageDto> SearchWorkItems(
        [Description("Only items in these Spaces. Use list_spaces to find valid ids. Omit for every Space.")] IReadOnlyList<Guid>? spaceIds = null,
        [Description("Words that must all appear, as words or the start of one, in the title, description or When, ignoring case (e.g. \"paint hall\"). Just \"#123\" looks up that Number instead.")] string? text = null,
        [Description("Only the work item with this Number (the #N people use).")] int? number = null,
        [Description("Only items whose status is in one of these categories: ToDo, Doing, Done.")] IReadOnlyList<StatusCategory>? statusCategories = null,
        [Description("Only items assigned to this user. Use list_users to find valid ids.")] Guid? assigneeId = null,
        [Description("Only items with these tags, ignoring case. Tags are shared across Spaces.")] IReadOnlyList<string>? tags = null,
        [Description("Any (the default) for items with at least one of the tags, All for items with every one.")] TagMatch tagMatch = TagMatch.Any,
        [Description("Only items of one of these Types. Use list_work_item_types to find valid ids.")] IReadOnlyList<Guid>? typeIds = null,
        [Description("Start of a date window, inclusive (yyyy-MM-dd). With a window, only items whose dates overlap it are listed, plus any undated item with a descendant that matches every argument; undated items are otherwise left out.")] DateOnly? from = null,
        [Description("End of the date window, inclusive (yyyy-MM-dd).")] DateOnly? to = null,
        [Description("True for only the Backlog: items that aren't Done and have no dates.")] bool backlogOnly = false,
        [Description("Only items last updated at or after this moment, as an ISO 8601 timestamp with offset. See list_changes_since for what changed.")] DateTimeOffset? updatedSince = null,
        [Description("The nextCursor from the previous page. Omit for the first page.")] string? cursor = null,
        [Description("Items per page, 1 to 1000. Defaults to 50.")] int limit = WorkItemQuery.DefaultLimit,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var page = await _query.QuerySummariesAsync(
                new WorkItemQuery(
                    SpaceIds: spaceIds,
                    Text: text,
                    Number: number,
                    StatusCategories: statusCategories,
                    AssigneeId: assigneeId,
                    Tags: tags,
                    TagMatch: tagMatch,
                    TypeIds: typeIds,
                    From: from,
                    To: to,
                    BacklogOnly: backlogOnly,
                    UpdatedSince: updatedSince,
                    Cursor: cursor,
                    Limit: limit),
                ct);
            return WorkItemSummaryPageDto.FromPage(page);
        });
}
