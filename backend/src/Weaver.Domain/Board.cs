namespace Weaver.Domain;

/// <summary>
/// A saved board view. Swimlanes are the direct children of <see cref="ScopeItemId"/>
/// (or top-level items when null); cards are each swimlane's direct children, grouped
/// into columns by status.
/// </summary>
public class Board
{
    public const int NameMaxLength = 200;

    public Guid Id { get; set; }

    public required string Name { get; set; }

    public Guid? ScopeItemId { get; set; }

    public WorkItem? ScopeItem { get; set; }
}
