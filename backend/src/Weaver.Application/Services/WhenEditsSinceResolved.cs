using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// The one rule for a When "edited since resolved" (ADR-0010, ticket 49), as a query so every
/// reader applies it the same way. An item is edited since resolved when it has a When and
/// dates, and a human's When edit in its history is newer than the last change to its start or
/// end date. An agent's When edit doesn't count, and an item resolved only by a blocked-by link
/// never qualifies, because agents never remove links.
/// </summary>
internal static class WhenEditsSinceResolved
{
    /// <summary>
    /// Not <c>IgnoreQueryFilters</c>: that would lift the Trash filter from any query this is
    /// part of. A query that reads trashed rows lifts it from this part too, so it still agrees
    /// with the rows it reads. History ids order entries, even ones saved in the same instant.
    /// </summary>
    public static IQueryable<Guid> WhenEditedSinceResolvedIds(this IWeaverDbContext db) =>
        db.WorkItems
            .Where(w => w.When != null && (w.StartDate != null || w.EndDate != null))
            .Where(w => db.WorkItemHistory.Any(edit =>
                edit.WorkItemId == w.Id
                && edit.Field == WorkItemField.When
                && edit.Account!.Kind == UserKind.Human
                && !db.WorkItemHistory.Any(dates =>
                    dates.WorkItemId == w.Id
                    && (dates.Field == WorkItemField.StartDate || dates.Field == WorkItemField.EndDate)
                    && dates.Id > edit.Id)))
            .Select(w => w.Id);
}
