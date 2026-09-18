namespace Weaver.Domain;

public class Comment
{
    public Guid Id { get; set; }

    public Guid WorkItemId { get; set; }

    public Guid AuthorUserId { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Null until the comment is edited.</summary>
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public WorkItem? WorkItem { get; set; }

    public User? AuthorUser { get; set; }
}
