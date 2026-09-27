namespace Weaver.Domain;

/// <summary>
/// A saved board view. Swimlanes are the direct children of <see cref="ScopeItemId"/>
/// (or top-level items when null); cards are each swimlane's direct children, grouped
/// into columns by status.
/// </summary>
public class Board
{
    public const int NameMaxLength = 200;

    /// <summary>For EF Core.</summary>
    private Board()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Guid? ScopeItemId { get; private set; }

    public WorkItem? ScopeItem { get; private set; }

    public static Board Create(string name, Guid? scopeItemId)
    {
        TextValidation.RequireText(name, "Board name", NameMaxLength);
        return new Board { Id = Guid.NewGuid(), Name = name, ScopeItemId = scopeItemId };
    }
}
