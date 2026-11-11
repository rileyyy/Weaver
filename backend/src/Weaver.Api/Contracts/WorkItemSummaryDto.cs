using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A work item in brief, with names instead of ids. For a purged item only the id, title and
/// last Space are known, and the rest is null. <see cref="TrashedAtUtc"/> is set for an item in
/// the Trash. <see cref="ParentId"/> is null for a top-level item. Full detail comes from
/// <c>get_work_item</c>. <see cref="When"/> is the item's When phrase, which stays after it's
/// resolved. <see cref="CompletionDate"/> is the day the item was finished, set while it's Done.
/// <see cref="WhenEditedSinceResolved"/> is true when a human edited the When after the item's
/// dates were last set: its dates are kept until an agent resolves it again.
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
    string? When,
    DateOnly? CompletionDate,
    bool WhenEditedSinceResolved)
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
        summary.When,
        summary.CompletionDate,
        summary.WhenEditedSinceResolved);

    public static IReadOnlyList<WorkItemSummaryDto> FromSummaries(IEnumerable<WorkItemSummary> summaries) =>
        summaries.Select(FromSummary).ToList();
}
