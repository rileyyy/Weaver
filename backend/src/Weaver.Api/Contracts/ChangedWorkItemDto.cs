using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A work item that changed since T, with its history entries since then, oldest first.
/// <see cref="Deleted"/> is true when the item no longer exists.
/// </summary>
public record ChangedWorkItemDto(WorkItemSummaryDto Item, bool Deleted, IReadOnlyList<WorkItemHistoryEntryDto> Changes)
{
    public static ChangedWorkItemDto FromChange(ChangedWorkItem change) => new(
        WorkItemSummaryDto.FromSummary(change.Item),
        change.Deleted,
        change.Changes.Select(WorkItemHistoryEntryDto.FromView).ToList());
}
