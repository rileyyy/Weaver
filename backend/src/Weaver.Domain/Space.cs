namespace Weaver.Domain;

/// <summary>
/// A top-level container such as Home, Village or Software, holding a forest of work-item
/// trees. Every work item belongs to exactly one Space, and reparenting never crosses one
/// (ADR-0001).
/// </summary>
public class Space : IHasCreatedAt
{
    public const int NameMaxLength = 100;

    /// <summary>For EF Core.</summary>
    private Space()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Trimmed, and unique across Spaces.</summary>
    public string Name { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Space Create(string name) => new() { Id = Guid.NewGuid(), Name = ValidName(name) };

    public IReadOnlyList<FieldChange> Rename(string name)
    {
        var changed = new ChangedFields();
        Name = changed.Set(SpaceHistoryField.Name, Name, ValidName(name));
        return changed.Changes;
    }

    private static string ValidName(string name)
    {
        TextValidation.RequireText(name, "Space name", NameMaxLength);
        return name.Trim();
    }
}
