namespace Weaver.Domain;

/// <summary>
/// A configurable priority of one Space (a new Space starts with Low / Medium / High / Urgent),
/// so priorities can be renamed, reordered or added without a schema change.
/// <see cref="LookupValue.Order"/> runs from least to most urgent.
/// </summary>
public class WorkItemPriority : SpaceLookupValue
{
    /// <summary>For EF Core.</summary>
    private WorkItemPriority()
    {
    }

    private WorkItemPriority(Guid spaceId, string name, int order) : base(spaceId, name, order)
    {
    }

    public static WorkItemPriority Create(Guid spaceId, string name, int order) => new(spaceId, name, order);
}
