namespace Weaver.Api.Contracts;

/// <summary>
/// A work item with whatever context around it was asked for. A list that wasn't asked for is
/// null (and so left out over MCP); one that was asked for but has nothing in it is empty.
/// <see cref="Ancestors"/> run from the top-level item down to the parent. <see cref="History"/>
/// is newest first and one page long; <see cref="NextHistoryOffset"/> is where the next page
/// starts, null when there isn't one.
/// </summary>
public record WorkItemContextDto(
    WorkItemDto Item,
    IReadOnlyList<WorkItemSummaryDto>? Ancestors = null,
    IReadOnlyList<WorkItemSummaryDto>? Children = null,
    IReadOnlyList<CommentDto>? Comments = null,
    IReadOnlyList<WorkItemLinkDto>? Links = null,
    IReadOnlyList<WorkItemUrlLinkDto>? UrlLinks = null,
    IReadOnlyList<WorkItemHistoryEntryDto>? History = null,
    int? NextHistoryOffset = null,
    IReadOnlyList<AttachmentDto>? Attachments = null);
