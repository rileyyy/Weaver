namespace Weaver.Application.Services;

/// <summary>
/// A work item that changed since T, with its history entries since then, oldest first.
/// <see cref="Deleted"/> means the item is gone now; its summary then holds only what history
/// still knows (id, title and last Space).
/// </summary>
public sealed record ChangedWorkItem(WorkItemSummary Item, bool Deleted, IReadOnlyList<WorkItemHistoryView> Changes);
