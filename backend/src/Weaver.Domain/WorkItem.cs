namespace Weaver.Domain;

public enum WorkItemPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

public class WorkItem
{
    public const int TitleMaxLength = 500;
    public const int MaxTags = 20;
    public const int TagMaxLength = 50;

    public Guid Id { get; set; }

    /// <summary>
    /// A short, sequential, human-facing identifier (e.g. Azure DevOps'
    /// "#1234") — database-generated (see <c>WorkItemConfiguration</c>),
    /// never set by application code. <see cref="Id"/> remains the real
    /// primary key everywhere else (foreign keys, API routes); this exists
    /// purely for display.
    /// </summary>
    public int Number { get; set; }

    public Guid? ParentId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public Guid StatusId { get; set; }

    /// <summary>
    /// What kind of item this is (e.g. Project/Goal/Task) — a label from the
    /// configurable <see cref="WorkItemLayer"/> table, not a constraint on
    /// the parent/child tree. Nullable: existing/ad-hoc items don't require
    /// categorization.
    /// </summary>
    public Guid? LayerId { get; set; }

    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;

    public Guid? AssignedToUserId { get; set; }

    /// <summary>
    /// Short, free-text labels (e.g. "urgent", "needs review") — zero or
    /// more, never null. Set as a whole via <c>SetTagsAsync</c>, not
    /// individually added/removed server-side.
    /// </summary>
    public List<string> Tags { get; set; } = new();

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

    public WorkItemLayer? Layer { get; set; }

    public User? AssignedToUser { get; set; }
}
