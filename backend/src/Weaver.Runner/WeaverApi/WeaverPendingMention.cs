namespace Weaver.Runner.WeaverApi;

/// <summary>The parts of Weaver's <c>PendingMentionDto</c> the runner reads: the asking comment and its item.</summary>
public sealed record WeaverPendingMention(WeaverComment Comment, Guid WorkItemId, int WorkItemNumber, string WorkItemTitle);
