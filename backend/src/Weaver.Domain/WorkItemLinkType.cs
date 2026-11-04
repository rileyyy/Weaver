namespace Weaver.Domain;

/// <summary>
/// How a <see cref="WorkItemLink"/> is stored. <see cref="Blocks"/> and <see cref="Duplicates"/>
/// are directional: the link's <see cref="WorkItemLink.WorkItemId"/> blocks (or duplicates) its
/// <see cref="WorkItemLink.LinkedWorkItemId"/>. Each side reads a link as a
/// <see cref="WorkItemLinkRelation"/>.
/// </summary>
public enum WorkItemLinkType
{
    Related,
    Blocks,
    Duplicates,
}
