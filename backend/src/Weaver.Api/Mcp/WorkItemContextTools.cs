using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemContextTools(
    IWorkItemService workItems,
    WorkItemDtoMapper dtos,
    ICommentService comments,
    IWorkItemLinkService links,
    IWorkItemUrlLinkService urlLinks,
    IWorkItemHistoryService history,
    IWorkItemAttachmentService attachments)
{
    private readonly IWorkItemService _workItems = workItems;
    private readonly WorkItemDtoMapper _dtos = dtos;
    private readonly ICommentService _comments = comments;
    private readonly IWorkItemLinkService _links = links;
    private readonly IWorkItemUrlLinkService _urlLinks = urlLinks;
    private readonly IWorkItemHistoryService _history = history;
    private readonly IWorkItemAttachmentService _attachments = attachments;

    [McpServerTool(Name = "get_work_item_context", ReadOnly = true)]
    [Description("Gets a work item in full detail plus the context around it that you ask for, in one call, " +
        "by id or by Number (the #N people use). Give exactly one of id and number. include picks what to add: " +
        "ancestors (summaries from the top-level item down to the parent), children (summaries of the direct " +
        "children, in board order), comments (oldest first), links (links to other work items as links, and " +
        "URL links as urlLinks), history (newest first, one page: see historyOffset and historyLimit) and " +
        "attachments (metadata only; read one with get_work_item_attachment). Only what you include is " +
        "returned. Items in the Trash are left out. Fields with no value are left out.")]
    public Task<WorkItemContextDto> GetWorkItemContext(
        [Description(WorkItemReference.IdDescription)] Guid? id = null,
        [Description(WorkItemReference.NumberDescription)] int? number = null,
        [Description("What to add around the item, any of: ancestors, children, comments, links, history, " +
            "attachments. Omit for the item alone.")] IReadOnlyList<string>? include = null,
        [Description("With history included: how many of the newest entries to skip. Defaults to 0; when more " +
            "history exists, the result's nextHistoryOffset is the value to send for the next page.")] int historyOffset = 0,
        [Description("With history included: the most entries to return, 1 to 200. Defaults to 20.")]
        int historyLimit = WorkItemHistoryService.DefaultPageSize,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var includes = WorkItemContextIncludes.Parse(include);
            var item = await _workItems.FindByIdOrNumberAsync(id, number, ct);

            bool Includes(WorkItemContextInclude what) => includes.HasFlag(what);
            var historyPage = Includes(WorkItemContextInclude.History)
                ? await _history.ListPageForWorkItemAsync(item.Id, historyOffset, historyLimit, ct)
                : null;

            return new WorkItemContextDto(
                await _dtos.ToDtoAsync(item, ct),
                Ancestors: Includes(WorkItemContextInclude.Ancestors)
                    ? WorkItemSummaryDto.FromSummaries(await _workItems.GetAncestorSummariesAsync(item.Id, ct))
                    : null,
                Children: Includes(WorkItemContextInclude.Children)
                    ? WorkItemSummaryDto.FromSummaries(await _workItems.GetChildSummariesAsync(item.Id, ct: ct))
                    : null,
                Comments: Includes(WorkItemContextInclude.Comments)
                    ? (await _comments.ListForWorkItemAsync(item.Id, ct)).Select(CommentDto.FromView).ToList()
                    : null,
                Links: Includes(WorkItemContextInclude.Links)
                    ? (await _links.ListForWorkItemAsync(item.Id, ct)).Select(WorkItemLinkDto.FromView).ToList()
                    : null,
                UrlLinks: Includes(WorkItemContextInclude.Links)
                    ? (await _urlLinks.ListForWorkItemAsync(item.Id, ct)).Select(WorkItemUrlLinkDto.FromView).ToList()
                    : null,
                History: historyPage?.Entries.Select(WorkItemHistoryEntryDto.FromView).ToList(),
                NextHistoryOffset: historyPage?.NextOffset,
                Attachments: Includes(WorkItemContextInclude.Attachments)
                    ? (await _attachments.ListForWorkItemAsync(item.Id, ct)).Select(AttachmentDto.FromView).ToList()
                    : null);
        });
}
