using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>Work items with a row (live or trashed) as <see cref="WorkItemSummary"/>, in one projection with no lookup per item.</summary>
internal static class WorkItemSummaries
{
    public static IQueryable<WorkItemSummary> Summarize(this IQueryable<WorkItem> items) =>
        items.Select(w => new WorkItemSummary(
            w.Id,
            w.Number,
            w.Title,
            w.SpaceId,
            w.Space!.Name,
            w.ParentId,
            w.Status!.Name,
            w.Type == null ? null : w.Type.Name,
            w.StartDate,
            w.EndDate,
            w.AssignedToUser == null ? null : w.AssignedToUser.Username,
            // Filtered here rather than left to the Trash query filter: a caller that reads
            // trashed rows lifts that filter from navigations too.
            w.Children.Count(c => c.TrashedAtUtc == null),
            w.TrashedAtUtc,
            w.When,
            w.CompletionDate));
}
