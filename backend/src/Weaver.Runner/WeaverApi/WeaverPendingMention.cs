namespace Weaver.Runner.WeaverApi;

/// <summary>
/// The parts of Weaver's <c>PendingMentionDto</c> the runner reads: the asking comment and its
/// item. <see cref="MentionedAtUtc"/> is when the mention appeared, later than the comment if an
/// edit added it.
/// </summary>
public sealed record WeaverPendingMention(
    WeaverComment Comment,
    Guid WorkItemId,
    int WorkItemNumber,
    string WorkItemTitle,
    DateTimeOffset MentionedAtUtc);
