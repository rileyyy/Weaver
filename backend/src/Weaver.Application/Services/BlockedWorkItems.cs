using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>Which work items a live item blocks, as a query, for every reader that needs blockers.</summary>
internal static class BlockedWorkItems
{
    /// <summary>
    /// The blocker's Trash state is checked here as well as by the links' Trash filter, since a
    /// query that reads trashed rows lifts that filter from the whole query, this part included.
    /// </summary>
    public static IQueryable<Guid> BlockedIds(this IQueryable<WorkItemLink> links) =>
        links
            .Where(l => l.Type == WorkItemLinkType.Blocks && l.WorkItem!.TrashedAtUtc == null)
            .Select(l => l.LinkedWorkItemId);
}
