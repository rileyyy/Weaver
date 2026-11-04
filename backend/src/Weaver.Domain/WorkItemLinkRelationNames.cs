namespace Weaver.Domain;

/// <summary>
/// The <see cref="WorkItemLinkRelation"/> names for error messages, so a new relation can't be
/// left out of the list a caller is told to choose from.
/// </summary>
public static class WorkItemLinkRelationNames
{
    public static readonly string All = string.Join(", ", Enum.GetNames<WorkItemLinkRelation>());
}
