using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A node in the work item tree. State changes go through methods so the rules below hold
/// for every caller (REST, MCP, background work), not just the ones a service remembers to
/// check. Rules that need other rows (cycle detection, existence of the new parent, rank
/// neighbours) stay in the service, which then calls these methods.
/// </summary>
public class WorkItem : IHasUpdatedAt
{
    public const int TitleMaxLength = 500;
    public const int MaxTags = 20;
    public const int TagMaxLength = 50;

    private List<string> _tags = [];

    /// <summary>For EF Core, which materializes through private setters.</summary>
    private WorkItem()
    {
        Title = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// A short, sequential, human-facing identifier (e.g. Azure DevOps'
    /// "#1234") — database-generated (see <c>WorkItemConfiguration</c>),
    /// never set by application code. <see cref="Id"/> remains the real
    /// primary key everywhere else (foreign keys, API routes); this exists
    /// purely for display.
    /// </summary>
    public int Number { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public Guid StatusId { get; private set; }

    /// <summary>
    /// What kind of item this is (e.g. Project/Goal/Task) — a label from the
    /// configurable <see cref="WorkItemLayer"/> table, not a constraint on
    /// the parent/child tree. Nullable: existing/ad-hoc items don't require
    /// categorization.
    /// </summary>
    public Guid? LayerId { get; private set; }

    public WorkItemPriority Priority { get; private set; } = WorkItemPriority.Medium;

    public Guid? AssignedToUserId { get; private set; }

    /// <summary>
    /// Short, free-text labels (e.g. "urgent", "needs review") — zero or
    /// more, never null. Replaced as a whole via <see cref="SetTags"/>.
    /// </summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => _tags = [.. value];
    }

    public double Rank { get; private set; }

    /// <summary>
    /// When this item is scheduled to start/end. Either may be set without
    /// the other — an open start or end is treated as unbounded on that
    /// side by time-frame filters, not as "never scheduled."
    /// </summary>
    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// The repeating work item this one was generated from, and the occurrence date it was
    /// generated for. Both null for items created by hand. Together they're unique, which is
    /// what stops generation from creating the same occurrence twice.
    /// </summary>
    public Guid? RecurrenceSourceId { get; private set; }

    public DateOnly? RecurrenceDate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public WorkItem? Parent { get; private set; }

    public ICollection<WorkItem> Children { get; private set; } = new List<WorkItem>();

    public Status? Status { get; private set; }

    public WorkItemLayer? Layer { get; private set; }

    public User? AssignedToUser { get; private set; }

    public WorkItem? RecurrenceSource { get; private set; }

    public static WorkItem Create(
        string title,
        string? description,
        Guid? parentId,
        Guid statusId,
        double rank,
        Guid? layerId = null,
        WorkItemPriority priority = WorkItemPriority.Medium)
    {
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            StatusId = statusId,
            Rank = rank,
        };
        item.UpdateDetails(title, description, layerId, priority);
        return item;
    }

    /// <summary>
    /// The descriptive fields share no invariant with status, parent, schedule or assignee,
    /// so they are safe to change together.
    /// </summary>
    public void UpdateDetails(string title, string? description, Guid? layerId, WorkItemPriority priority)
    {
        TextValidation.RequireText(title, "Title", TitleMaxLength);

        Title = title;
        Description = description;
        LayerId = layerId;
        Priority = priority;
    }

    /// <summary>
    /// Moves the item to another column. Never touches <see cref="ParentId"/>: that is what
    /// guarantees a drag between columns can never reparent the item.
    /// </summary>
    public void MoveToStatus(Guid statusId, double rank)
    {
        StatusId = statusId;
        Rank = rank;
    }

    /// <summary>
    /// Moves the item under another parent, keeping its status. The caller must already have
    /// checked the new parent isn't one of this item's descendants; an item being its own
    /// parent is rejected here.
    /// </summary>
    public void MoveToParent(Guid? parentId, double rank)
    {
        if (parentId == Id)
        {
            throw new CyclicParentException(Id, Id);
        }

        ParentId = parentId;
        Rank = rank;
    }

    /// <summary>Changes the item's place within its current board cell.</summary>
    public void Reposition(double rank)
    {
        Rank = rank;
    }

    public void Reschedule(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate is not null && endDate is not null && startDate > endDate)
        {
            throw new InvalidWorkItemScheduleException(Id);
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public void AssignTo(Guid? userId)
    {
        AssignedToUserId = userId;
    }

    /// <summary>
    /// A copy of this item for one occurrence of a repetition: same descriptive fields,
    /// assignee and tags, placed under <paramref name="parentId"/> and scheduled on
    /// <paramref name="date"/>. Comments, links, schedule and repetition are not copied.
    /// </summary>
    public WorkItem CopyForOccurrence(Guid? parentId, Guid statusId, double rank, DateOnly date)
    {
        var copy = Create(Title, Description, parentId, statusId, rank, LayerId, Priority);
        copy.AssignedToUserId = AssignedToUserId;
        copy._tags = [.. _tags];
        copy.StartDate = date;
        copy.EndDate = date;
        return copy;
    }

    /// <summary>
    /// Marks this item as the occurrence of <paramref name="sourceId"/> for
    /// <paramref name="date"/>; the pair is unique, so an occurrence can't be generated twice.
    /// </summary>
    public void MarkAsOccurrenceOf(Guid sourceId, DateOnly date)
    {
        RecurrenceSourceId = sourceId;
        RecurrenceDate = date;
    }

    /// <summary>
    /// Replaces the full tag list. Tags are trimmed and de-duplicated case-insensitively,
    /// keeping the first spelling; a blank tag is rejected.
    /// </summary>
    public void SetTags(IEnumerable<string> tags)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();
            if (trimmed.Length == 0)
            {
                throw new InvalidWorkItemTagException(Id);
            }

            TextValidation.RequireMaxLength(trimmed, "Each tag", TagMaxLength);

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        if (normalized.Count > MaxTags)
        {
            throw new DomainValidationException($"A work item can have at most {MaxTags} tags.");
        }

        _tags = normalized;
    }
}
