namespace Weaver.Application.Services;

/// <summary>
/// A link as seen from one work item: <see cref="LinkedWorkItemId"/> and
/// <see cref="LinkedWorkItemTitle"/> are always the other side, whichever item created it.
/// </summary>
public record WorkItemLinkView(Guid Id, Guid LinkedWorkItemId, string LinkedWorkItemTitle);
