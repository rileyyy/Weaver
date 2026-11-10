using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A work item in brief, with names instead of ids. For a purged item only the id, title and
/// last Space are known, and the rest is null. <see cref="TrashedAtUtc"/> is set for an item in
/// the Trash. <see cref="ParentId"/> is null for a top-level item. Full detail comes from
/// <c>get_work_item</c>. <see cref="When"/> is the item's When phrase, which stays after it's
/// resolved.
/// </summary>
public record WorkItemSummaryDto(
    Guid Id,
    int? Number,
    string Title,
    Guid? SpaceId,
    string? SpaceName,
    Guid? ParentId,
    string? StatusName,
    string? TypeName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? AssigneeName,
    int? ChildCount,
    DateTimeOffset? TrashedAtUtc,
    string? When)
{
    public static WorkItemSummaryDto FromSummary(WorkItemSummary summary) => new(
        summary.Id,
        summary.Number,
        summary.Title,
        summary.SpaceId,
        summary.SpaceName,
        summary.ParentId,
        summary.StatusName,
        summary.TypeName,
        summary.StartDate,
        summary.EndDate,
        summary.AssigneeName,
        summary.ChildCount,
        summary.TrashedAtUtc,
        summary.When);

    public static IReadOnlyList<WorkItemSummaryDto> FromSummaries(IEnumerable<WorkItemSummary> summaries) =>
        summaries.Select(FromSummary).ToList();
}
