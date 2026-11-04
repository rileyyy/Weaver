using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A typed association between two work items, possibly in different Spaces — not hierarchy
/// (that's ParentId). Directional types are stored once, from <see cref="WorkItemId"/> to
/// <see cref="LinkedWorkItemId"/>; <see cref="RelationFrom"/> reads the link from either side.
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

    public WorkItemLinkType Type { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public WorkItem? WorkItem { get; private set; }

    public WorkItem? LinkedWorkItem { get; private set; }

    /// <summary>
    /// A link from <paramref name="workItemId"/>'s side: <paramref name="relation"/> is how it
    /// relates to <paramref name="otherWorkItemId"/>. A blocked-by or duplicated-by link is stored
    /// the other way round, so every blocks link reads the same way in the database.
    /// </summary>
    public static WorkItemLink Create(Guid workItemId, Guid otherWorkItemId, WorkItemLinkRelation relation)
    {
        if (workItemId == otherWorkItemId)
        {
            throw new SelfWorkItemLinkException(workItemId);
        }

        var (type, reversed) = relation switch
        {
            WorkItemLinkRelation.Related => (WorkItemLinkType.Related, false),
            WorkItemLinkRelation.Blocks => (WorkItemLinkType.Blocks, false),
            WorkItemLinkRelation.BlockedBy => (WorkItemLinkType.Blocks, true),
            WorkItemLinkRelation.Duplicates => (WorkItemLinkType.Duplicates, false),
            WorkItemLinkRelation.DuplicatedBy => (WorkItemLinkType.Duplicates, true),
            _ => throw new DomainValidationException($"Link type {relation} is not one of Related, Blocks, BlockedBy, Duplicates or DuplicatedBy."),
        };

        return new WorkItemLink
        {
            Id = Guid.NewGuid(),
            WorkItemId = reversed ? otherWorkItemId : workItemId,
            LinkedWorkItemId = reversed ? workItemId : otherWorkItemId,
            Type = type,
        };
    }

    /// <summary>The item on the other side of the link from <paramref name="workItemId"/>'s view.</summary>
    public Guid OtherSideOf(Guid workItemId) => WorkItemId == workItemId ? LinkedWorkItemId : WorkItemId;

    /// <summary>How <paramref name="workItemId"/>, one of the link's two items, relates to the other.</summary>
    public WorkItemLinkRelation RelationFrom(Guid workItemId)
    {
        if (workItemId != WorkItemId && workItemId != LinkedWorkItemId)
        {
            throw new ArgumentException($"Work item {workItemId} is not on link {Id}.", nameof(workItemId));
        }

        return RelationFrom(Type, isSource: workItemId == WorkItemId);
    }

    /// <summary>
    /// The relation a stored link of <paramref name="type"/> reads as from its source
    /// (<see cref="WorkItemId"/>) or its target, for readers that project rows instead of entities.
    /// </summary>
    public static WorkItemLinkRelation RelationFrom(WorkItemLinkType type, bool isSource) => type switch
    {
        WorkItemLinkType.Blocks => isSource ? WorkItemLinkRelation.Blocks : WorkItemLinkRelation.BlockedBy,
        WorkItemLinkType.Duplicates => isSource ? WorkItemLinkRelation.Duplicates : WorkItemLinkRelation.DuplicatedBy,
        _ => WorkItemLinkRelation.Related,
    };

    /// <summary>
    /// Whether both this and <paramref name="other"/> are blocks links between the same two items
    /// in opposite directions: together they'd have each item wait on the other.
    /// </summary>
    public bool IsReverseBlocksOf(WorkItemLink other) =>
        Type == WorkItemLinkType.Blocks &&
        other.Type == WorkItemLinkType.Blocks &&
        WorkItemId == other.LinkedWorkItemId &&
        LinkedWorkItemId == other.WorkItemId;
}
