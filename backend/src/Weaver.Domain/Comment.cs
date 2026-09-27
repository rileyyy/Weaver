using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

public class Comment : IHasCreatedAt
{
    public const int BodyMaxLength = 4000;

    /// <summary>For EF Core.</summary>
    private Comment()
    {
        Body = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid WorkItemId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null until the comment is edited.</summary>
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public WorkItem? WorkItem { get; private set; }

    public User? AuthorUser { get; private set; }

    public static Comment Create(Guid workItemId, Guid authorUserId, string body)
    {
        TextValidation.RequireText(body, "Comment", BodyMaxLength);
        return new Comment
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            AuthorUserId = authorUserId,
            Body = body,
        };
    }

    public void Edit(Guid requestingUserId, string body, DateTimeOffset now)
    {
        EnsureAuthoredBy(requestingUserId);
        TextValidation.RequireText(body, "Comment", BodyMaxLength);

        Body = body;
        UpdatedAtUtc = now;
    }

    /// <summary>Only a comment's author may edit or delete it.</summary>
    public void EnsureAuthoredBy(Guid userId)
    {
        if (AuthorUserId != userId)
        {
            throw new CommentAuthorMismatchException(Id);
        }
    }
}
