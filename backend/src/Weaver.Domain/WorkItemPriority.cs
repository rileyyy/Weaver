namespace Weaver.Domain;

/// <summary>
/// A configurable priority (seeded Low / Medium / High / Urgent) — a plain lookup table like
/// <see cref="WorkItemLayer"/>, so priorities can be renamed, reordered or added without a
/// schema change. <see cref="Order"/> runs from least to most urgent.
/// </summary>
public class WorkItemPriority
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public int Order { get; set; }
}
