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
    /// is written. It's built with initializers rather than constructors, the sides included, so a
    /// query can still filter and sort on the row's members (<see cref="BlocksLinkRow.Conflicts"/>,
    /// a side's Space) in the database.
    /// </summary>
    public static IQueryable<BlocksLinkRow> LiveBlocksLinkRows(this IQueryable<WorkItemLink> links) =>
        links.LiveBlocksLinks().Select(l => new BlocksLinkRow
        {
            Blocked = new BlocksLinkSide
            {
                Id = l.LinkedWorkItemId,
                Number = l.LinkedWorkItem!.Number,
                Title = l.LinkedWorkItem.Title,
                SpaceId = l.LinkedWorkItem.SpaceId,
                SpaceName = l.LinkedWorkItem.Space!.Name,
                StartDate = l.LinkedWorkItem.StartDate,
                EndDate = l.LinkedWorkItem.EndDate,
            },
            Blocker = new BlocksLinkSide
            {
                Id = l.WorkItemId,
                Number = l.WorkItem!.Number,
                Title = l.WorkItem.Title,
                SpaceId = l.WorkItem.SpaceId,
                SpaceName = l.WorkItem.Space!.Name,
                StartDate = l.WorkItem.StartDate,
                EndDate = l.WorkItem.EndDate,
            },
            // CompletionDate is set exactly while an item is Done, and reading it saves joining
            // both Statuses.
            Conflicts = l.LinkedWorkItem.StartDate < l.WorkItem.EndDate
                && l.LinkedWorkItem.CompletionDate == null
                && l.WorkItem.CompletionDate == null,
        });
}
