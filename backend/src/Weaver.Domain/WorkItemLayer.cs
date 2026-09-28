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
    }

    private WorkItemLayer(string name, int order) : base(name, order)
    {
    }

    public static WorkItemLayer Create(string name, int order) => new(name, order);
}
