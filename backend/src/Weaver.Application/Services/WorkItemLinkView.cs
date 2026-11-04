using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// A link as seen from one work item: <see cref="Type"/> is how that item relates to the other,
/// and the <c>LinkedWorkItem</c> fields are always the other side, whichever item created it.
/// </summary>
public record WorkItemLinkView(
    Guid Id,
    WorkItemLinkRelation Type,
    Guid LinkedWorkItemId,
    int LinkedWorkItemNumber,
    string LinkedWorkItemTitle,
    Guid LinkedWorkItemSpaceId);
