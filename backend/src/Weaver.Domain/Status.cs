using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

public class Status : LookupValue
{
    /// <summary>For EF Core.</summary>
    private Status()
    {
        Color = string.Empty;
    }

    private Status(string name, int order, StatusCategory category, string color) : base(name, order)
    {
        Color = string.Empty;
        Recategorize(category, color);
    }

    public StatusCategory Category { get; private set; }

    /// <summary>
    /// The color a column/badge for this status renders with, as a
    /// <c>#RRGGBB</c> hex string. Stored per-status (not derived from
    /// <see cref="Category"/>) so each status can have its own.
    /// </summary>
    public string Color { get; private set; }

    public static Status Create(string name, int order, StatusCategory category, string color) =>
        new(name, order, category, color);

    public void Recategorize(StatusCategory category, string color)
    {
        if (!Enum.IsDefined(category))
        {
            throw new DomainValidationException($"'{category}' is not a status category.");
        }

        var normalized = HexColor.Normalize(color);

        Category = category;
        Color = normalized;
    }
}
