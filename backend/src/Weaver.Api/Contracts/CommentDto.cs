using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record CommentDto(
    Guid Id,
    Guid WorkItemId,
    Guid AuthorUserId,
    string AuthorUsername,
    string Body,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    public static CommentDto FromEntity(Comment comment) => new(
        comment.Id,
        comment.WorkItemId,
        comment.AuthorUserId,
        comment.AuthorUser?.Username ?? string.Empty,
        comment.Body,
        comment.CreatedAtUtc,
        comment.UpdatedAtUtc);
}

public record CreateCommentRequest(string Body);

public record UpdateCommentRequest(string Body);
