namespace Weaver.Domain;

public class WorkItem
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public Guid StatusId { get; set; }

    public double Rank { get; set; }

    /// <summary>
    /// When this item is scheduled to start/end. Either may be set without
    /// the other — an open start or end is treated as unbounded on that
    /// side by time-frame filters, not as "never scheduled."
    /// </summary>
    public DateTimeOffset? StartDate { get; set; }

    public DateTimeOffset? EndDate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public uint Version { get; set; }

    public WorkItem? Parent { get; set; }

    public ICollection<WorkItem> Children { get; set; } = new List<WorkItem>();

    public Status? Status { get; set; }
}
