using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// The one work-item query every client uses: Flutter, MCP and, later, Views (ADR-0006).
/// </summary>
/// <remarks>
/// <para>
/// Pages in <see cref="WorkItem.Number"/> order. A Number never changes and new items get higher
/// ones, so items moving or being added between pages can't make a reader skip or repeat one,
/// which paging over the mutable rank could.
/// </para>
/// <para>
/// The date window follows ADR-0003: an item matches when it has a date and its dates overlap
/// the window, a missing start or end being open on that side. An item with no dates matches
/// only when one of its descendants does.
/// </para>
/// </remarks>
public interface IWorkItemQueryService
{
    /// <summary>
    /// One page of matching items. Throws <see cref="Weaver.Domain.Exceptions.DomainValidationException"/>
    /// for a bad limit, cursor or date window.
    /// </summary>
    Task<WorkItemQueryPage<WorkItem>> QueryAsync(WorkItemQuery query, CancellationToken ct = default);

    /// <summary>The same page as <see cref="QueryAsync"/>, in brief, for agents.</summary>
    Task<WorkItemQueryPage<WorkItemSummary>> QuerySummariesAsync(WorkItemQuery query, CancellationToken ct = default);
}
