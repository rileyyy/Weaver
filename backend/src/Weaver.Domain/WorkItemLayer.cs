namespace Weaver.Domain;

/// <summary>
/// A configurable label for "what kind of thing is this" (e.g. Project /
/// Goal / Task, mirroring the classic epic/feature/task idea) — a plain
/// lookup table, not a hierarchy constraint. <see cref="WorkItem.LayerId"/>
/// is nullable and never validated against a parent/child's own layer:
/// parent/child stays fluid regardless of what layers exist or how items
/// are labeled.
/// </summary>
public class WorkItemLayer : LookupValue
{
    /// <summary>For EF Core.</summary>
    private WorkItemLayer()
    {
        Color = string.Empty;
    }

    private WorkItemLayer(string name, int order, string color) : base(name, order)
    {
        Color = string.Empty;
        Recolor(color);
    }

    /// <summary>
    /// The <c>#RRGGBB</c> color that marks this layer's items on the board, so what kind of
    /// thing a card is can be told at a glance.
    /// </summary>
    public string Color { get; private set; }

    public static WorkItemLayer Create(string name, int order, string color) => new(name, order, color);

    public void Recolor(string color)
    {
        Color = HexColor.Normalize(color);
    }
}
