namespace Weaver.Domain;

/// <summary>
/// What kind of thing a work item is (e.g. Room, Chore, Bug), defined per Space (ADR-0002).
/// A label, not a hierarchy constraint: <see cref="WorkItem.TypeId"/> is nullable and never
/// validated against a parent's or child's own Type, so any Type can be nested under any other.
/// </summary>
public class WorkItemType : SpaceLookupValue
{
    /// <summary>For EF Core.</summary>
    private WorkItemType()
    {
        Color = string.Empty;
    }

    private WorkItemType(Guid spaceId, string name, int order, string color) : base(spaceId, name, order)
    {
        Color = string.Empty;
        Recolor(color);
    }

    /// <summary>
    /// The <c>#RRGGBB</c> color that marks this type's items on the board, so what kind of
    /// thing a card is can be told at a glance.
    /// </summary>
    public string Color { get; private set; }

    public static WorkItemType Create(Guid spaceId, string name, int order, string color) =>
        new(spaceId, name, order, color);

    public void Recolor(string color)
    {
        Color = HexColor.Normalize(color);
    }
}
