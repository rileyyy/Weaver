using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>Which work items a live item blocks, as a query, for every reader that needs blockers.</summary>
internal static class BlockedWorkItems
{
    /// <summary>
    /// The blocker's Trash state is checked here as well as by the links' Trash filter, since a
    /// query that reads trashed rows lifts that filter from the whole query, this part included.
    /// </summary>
    public static IQueryable<WorkItemLink> LiveBlocksLinks(this IQueryable<WorkItemLink> links) =>
        links.Where(l => l.Type == WorkItemLinkType.Blocks && l.WorkItem!.TrashedAtUtc == null);

    public static IQueryable<Guid> BlockedIds(this IQueryable<WorkItemLink> links) =>
        links.LiveBlocksLinks().Select(l => l.LinkedWorkItemId);

    /// <summary>
    /// Each live blocks link as a <see cref="BlocksLinkRow"/>, the one place the conflict rule
    /// is written. It's built with an initializer rather than a constructor so a query can still
    /// filter on <see cref="BlocksLinkRow.Conflicts"/> in the database.
    /// </summary>
    public static IQueryable<BlocksLinkRow> LiveBlocksLinkRows(this IQueryable<WorkItemLink> links) =>
        links.LiveBlocksLinks().Select(l => new BlocksLinkRow
        {
            BlockedId = l.LinkedWorkItemId,
            BlockedNumber = l.LinkedWorkItem!.Number,
            BlockedTitle = l.LinkedWorkItem.Title,
            BlockedSpaceId = l.LinkedWorkItem.SpaceId,
            BlockedSpaceName = l.LinkedWorkItem.Space!.Name,
            BlockedStartDate = l.LinkedWorkItem.StartDate,
            BlockedEndDate = l.LinkedWorkItem.EndDate,
            BlockerId = l.WorkItemId,
            BlockerNumber = l.WorkItem!.Number,
            BlockerTitle = l.WorkItem.Title,
            BlockerSpaceId = l.WorkItem.SpaceId,
            BlockerSpaceName = l.WorkItem.Space!.Name,
            BlockerStartDate = l.WorkItem.StartDate,
            BlockerEndDate = l.WorkItem.EndDate,
            // CompletionDate is set exactly while an item is Done, and reading it saves joining
            // both Statuses.
            Conflicts = l.LinkedWorkItem.StartDate < l.WorkItem.EndDate
                && l.LinkedWorkItem.CompletionDate == null
                && l.WorkItem.CompletionDate == null,
        });
}
