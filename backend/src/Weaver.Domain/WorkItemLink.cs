using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A flat, symmetric "related to" association between two work items — not
/// hierarchy (that's ParentId) and no relationship-type vocabulary (e.g.
/// "blocks"). Which item is <see cref="WorkItemId"/> vs.
/// <see cref="LinkedWorkItemId"/> only reflects creation order; either side
/// sees the other as "linked."
/// </summary>
public class WorkItemLink : IHasCreatedAt
{
    /// <summary>For EF Core.</summary>
    private WorkItemLink()
    {
    }

    public Guid Id { get; private set; }

    public Guid WorkItemId { get; private set; }

    public Guid LinkedWorkItemId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public WorkItem? WorkItem { get; private set; }

    public WorkItem? LinkedWorkItem { get; private set; }

    public static WorkItemLink Create(Guid workItemId, Guid linkedWorkItemId)
    {
        if (workItemId == linkedWorkItemId)
        {
            throw new SelfWorkItemLinkException(workItemId);
        }

        return new WorkItemLink { Id = Guid.NewGuid(), WorkItemId = workItemId, LinkedWorkItemId = linkedWorkItemId };
    }

    /// <summary>The item on the other side of the link from <paramref name="workItemId"/>'s view.</summary>
    public Guid OtherSideOf(Guid workItemId) => WorkItemId == workItemId ? LinkedWorkItemId : WorkItemId;
}
