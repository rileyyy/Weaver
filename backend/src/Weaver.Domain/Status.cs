namespace Weaver.Domain;

public enum StatusCategory
{
    ToDo,
    Doing,
    Done,
}

public class Status
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public int Order { get; set; }

    public StatusCategory Category { get; set; }

    /// <summary>
    /// The color a column/badge for this status renders with, as a
    /// <c>#RRGGBB</c> hex string. Stored per-status (not derived from
    /// <see cref="Category"/>) so it can be reconfigured per status later
    /// without a schema change.
    /// </summary>
    public required string Color { get; set; }
}
