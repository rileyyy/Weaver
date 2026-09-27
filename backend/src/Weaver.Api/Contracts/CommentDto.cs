using Weaver.Application.Services;

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
    public static CommentDto FromView(CommentView comment) => new(
        comment.Id,
        comment.WorkItemId,
        comment.AuthorUserId,
        comment.AuthorUsername,
        comment.Body,
        comment.CreatedAtUtc,
        comment.UpdatedAtUtc);
}
