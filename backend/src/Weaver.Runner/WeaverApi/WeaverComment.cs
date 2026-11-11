namespace Weaver.Runner.WeaverApi;

/// <summary>The parts of Weaver's <c>CommentDto</c> the runner reads.</summary>
public sealed record WeaverComment(Guid Id, Guid AuthorUserId, string AuthorUsername, string Body, DateTimeOffset CreatedAtUtc);
