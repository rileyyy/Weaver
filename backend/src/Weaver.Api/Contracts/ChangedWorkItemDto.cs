using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A work item that changed since T, with its history entries since then, oldest first.
/// <see cref="Deleted"/> is true when the item is deleted now (in the Trash, or purged);
/// <see cref="Restored"/> is true when it came back from the Trash since T and is live now.
/// </summary>
public record ChangedWorkItemDto(
    WorkItemSummaryDto Item,
    bool Deleted,
    bool Restored,
    IReadOnlyList<WorkItemHistoryEntryDto> Changes)
{
    public static ChangedWorkItemDto FromChange(ChangedWorkItem change) => new(
        WorkItemSummaryDto.FromSummary(change.Item),
        change.Deleted,
        change.Restored,
        change.Changes.Select(WorkItemHistoryEntryDto.FromView).ToList());
}
