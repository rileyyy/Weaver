using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A comment that mentions the caller and hasn't been answered yet. Replying with a comment on
/// <see cref="WorkItemId"/> clears it.
/// </summary>
public record PendingMentionDto(
    CommentDto Comment,
    Guid WorkItemId,
    int WorkItemNumber,
    string WorkItemTitle,
    DateTimeOffset MentionedAtUtc)
{
    public static PendingMentionDto FromView(PendingMention mention) => new(
        CommentDto.FromView(mention.Comment),
        mention.Comment.WorkItemId,
        mention.WorkItem.Number,
        mention.WorkItem.Title,
        mention.MentionedAtUtc);
}
