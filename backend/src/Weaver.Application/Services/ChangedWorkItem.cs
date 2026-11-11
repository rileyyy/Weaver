namespace Weaver.Application.Services;

/// <summary>
/// A work item that changed since T, or in one run, with those history entries, oldest first.
/// <see cref="Deleted"/> means the item is deleted now: in the Trash, with its summary read from
/// its row, or purged, when the summary holds only what history still knows (id, title and last
/// Space). <see cref="Restored"/> means it came back from the Trash in those entries and is live now. They
/// describe where the item ended up, so at most one is set however often it went back and forth;
/// the entries say what happened in between.
/// </summary>
public sealed record ChangedWorkItem(
    WorkItemSummary Item,
    bool Deleted,
    bool Restored,
    IReadOnlyList<WorkItemHistoryView> Changes);
