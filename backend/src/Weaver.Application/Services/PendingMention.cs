namespace Weaver.Application.Services;

/// <summary>
/// A mention still waiting for the mentioned account to reply: the comment that asked, with
/// its author, and the item it's on.
/// </summary>
public record PendingMention(CommentView Comment, WorkItemReference WorkItem, DateTimeOffset MentionedAtUtc);
