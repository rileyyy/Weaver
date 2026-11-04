namespace Weaver.Domain;

/// <summary>
/// A <see cref="WorkItemLink"/> as one of its items sees it: how this item relates to the other.
/// A blocks link reads <see cref="Blocks"/> from the blocker and <see cref="BlockedBy"/> from the
/// item it blocks, though it's stored once.
/// </summary>
public enum WorkItemLinkRelation
{
    Related,
    Blocks,
    BlockedBy,
    Duplicates,
    DuplicatedBy,
}
