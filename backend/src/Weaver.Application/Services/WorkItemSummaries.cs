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
            w.Status!.Name,
            w.Type == null ? null : w.Type.Name,
            w.StartDate,
            w.EndDate,
            w.AssignedToUser == null ? null : w.AssignedToUser.Username,
            // EF applies the Trash filter to the navigation too, so trashed children aren't counted.
            w.Children.Count(),
            w.TrashedAtUtc));
}
