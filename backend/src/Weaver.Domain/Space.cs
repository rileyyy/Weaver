using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A top-level container such as Home, Village or Software, holding a forest of work-item
/// trees. Every work item belongs to exactly one Space, and reparenting never crosses one
/// (ADR-0001).
/// </summary>
public class Space : IHasCreatedAt
{
    public const int NameMaxLength = 100;
    public const int DefaultDoneHidingDays = 14;
    public const int MinDoneHidingDays = 1;
    public const int MaxDoneHidingDays = 3650;

    /// <summary>For EF Core.</summary>
    private Space()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Trimmed, and unique across Spaces.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// How many days a Done item stays on the Board after its completion date, counted in
    /// calendar days in the server's time zone. Each Space has its own, since a household Space
    /// and a software one finish work at different paces.
    /// </summary>
    public int DoneHidingDays { get; private set; } = DefaultDoneHidingDays;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Space Create(string name) => new() { Id = Guid.NewGuid(), Name = ValidName(name) };

    public IReadOnlyList<FieldChange> Rename(string name)
    {
        var changed = new ChangedFields();
        Name = changed.Set(SpaceHistoryField.Name, Name, ValidName(name));
        return changed.Changes;
    }

    public IReadOnlyList<FieldChange> SetDoneHidingPeriod(int days)
    {
        if (days is < MinDoneHidingDays or > MaxDoneHidingDays)
        {
            throw new DomainValidationException(
                $"The done-hiding period must be between {MinDoneHidingDays} and {MaxDoneHidingDays} days.");
        }

        var changed = new ChangedFields();
        DoneHidingDays = changed.Set(SpaceHistoryField.DoneHidingDays, DoneHidingDays, days);
        return changed.Changes;
    }

    private static string ValidName(string name)
    {
        TextValidation.RequireText(name, "Space name", NameMaxLength);
        return name.Trim();
    }
}
