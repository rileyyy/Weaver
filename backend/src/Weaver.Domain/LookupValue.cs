namespace Weaver.Domain;

/// <summary>
/// A configurable, ordered value that work items point at: statuses, layers and priorities.
/// Admins add, rename, reorder and delete these at runtime, so the name and order rules live
/// here rather than in each service.
/// </summary>
public abstract class LookupValue
{
    public const int NameMaxLength = 100;

    /// <summary>For EF Core.</summary>
    protected LookupValue()
    {
        Name = string.Empty;
    }

    protected LookupValue(string name, int order)
    {
        Id = Guid.NewGuid();
        Name = string.Empty;
        Rename(name);
        Order = order;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Display position; unique within one kind of lookup.</summary>
    public int Order { get; private set; }

    public void Rename(string name)
    {
        TextValidation.RequireText(name, "Name", NameMaxLength);
        Name = name.Trim();
    }

    public void MoveTo(int order)
    {
        Order = order;
    }
}
