namespace Weaver.Domain;

/// <summary>
/// A flat, symmetric "related to" association between two work items — not
/// hierarchy (that's ParentId) and no relationship-type vocabulary (e.g.
/// "blocks"). Which item is <see cref="WorkItemId"/> vs.
/// <see cref="LinkedWorkItemId"/> only reflects creation order; either side
/// sees the other as "linked."
/// </summary>
public class WorkItemLink
{
    public Guid Id { get; set; }

    public Guid WorkItemId { get; set; }

    public Guid LinkedWorkItemId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public WorkItem? WorkItem { get; set; }

    public WorkItem? LinkedWorkItem { get; set; }
}
