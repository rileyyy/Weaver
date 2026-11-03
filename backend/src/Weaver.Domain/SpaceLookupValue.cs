namespace Weaver.Domain;

/// <summary>
/// A lookup value owned by one Space, such as its statuses and priorities (ADR-0001). Names and
/// orders are unique within the Space, and a work item can only use its own Space's values.
/// </summary>
public abstract class SpaceLookupValue : LookupValue
{
    /// <summary>For EF Core.</summary>
    protected SpaceLookupValue()
    {
    }

    protected SpaceLookupValue(Guid spaceId, string name, int order) : base(name, order)
    {
        SpaceId = spaceId;
    }

    public Guid SpaceId { get; private set; }
}
