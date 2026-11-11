namespace Weaver.Domain;

/// <summary>
/// A comment naming an account by its <see cref="User.Handle"/>. Any account can be mentioned,
/// though only agents have handles today and only agent mentions are acted on (ticket 51).
/// </summary>
public class CommentMention : IHasCreatedAt
{
    /// <summary>For EF Core.</summary>
    private CommentMention()
    {
    }

    public Guid CommentId { get; private set; }

    public Guid MentionedUserId { get; private set; }

    /// <summary>
    /// When the mention first appeared, on save or on the edit that added it. A mention is
    /// answered by a reply after this moment, so editing the comment around a mention it already
    /// had doesn't ask again.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Comment? Comment { get; private set; }

    public User? MentionedUser { get; private set; }

    public static CommentMention Create(Guid commentId, Guid mentionedUserId) => new()
    {
        CommentId = commentId,
        MentionedUserId = mentionedUserId,
    };
}
