namespace Weaver.Application.Services;

/// <summary>
/// One page of a work item's history, newest first. <see cref="NextOffset"/> is the offset the
/// next page starts at, null when this is the last.
/// </summary>
public sealed record WorkItemHistoryPage(IReadOnlyList<WorkItemHistoryView> Entries, int? NextOffset);
