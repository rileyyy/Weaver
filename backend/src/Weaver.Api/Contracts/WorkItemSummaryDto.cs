using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A work item in brief, with names instead of ids. For a deleted item only the id, title and
/// last Space are known, and the rest is null. Full detail comes from <c>get_work_item</c>.
/// </summary>
public record WorkItemSummaryDto(
    Guid Id,
    int? Number,
    string Title,
    Guid? SpaceId,
    string? SpaceName,
    string? StatusName,
    string? LayerName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? AssigneeName,
    int? ChildCount)
{
    public static WorkItemSummaryDto FromSummary(WorkItemSummary summary) => new(
        summary.Id,
        summary.Number,
        summary.Title,
        summary.SpaceId,
        summary.SpaceName,
        summary.StatusName,
        summary.LayerName,
        summary.StartDate,
        summary.EndDate,
        summary.AssigneeName,
        summary.ChildCount);
}
