namespace Weaver.Domain;

/// <summary>
/// A configurable priority (seeded Low / Medium / High / Urgent) — a plain lookup table like
/// <see cref="WorkItemLayer"/>, so priorities can be renamed, reordered or added without a
/// schema change. <see cref="LookupValue.Order"/> runs from least to most urgent.
/// </summary>
public class WorkItemPriority : LookupValue
{
    /// <summary>For EF Core.</summary>
    private WorkItemPriority()
    {
    }

    private WorkItemPriority(string name, int order) : base(name, order)
    {
    }

    public static WorkItemPriority Create(string name, int order) => new(name, order);
}
