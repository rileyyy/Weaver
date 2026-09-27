namespace Weaver.Application.Services;

/// <summary>A comment as read, with its author's name resolved in the same query.</summary>
public record CommentView(
    Guid Id,
    Guid WorkItemId,
    Guid AuthorUserId,
    string AuthorUsername,
    string Body,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
