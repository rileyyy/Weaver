using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A link as seen from one particular work item: <see cref="Type"/> is how that item relates to
/// the other, and the <c>LinkedWorkItem</c> fields are always "the other side," regardless of
/// which of the two items created the link.
/// </summary>
public record WorkItemLinkDto(
    Guid Id,
    WorkItemLinkRelation Type,
    Guid LinkedWorkItemId,
    int LinkedWorkItemNumber,
    string LinkedWorkItemTitle,
    Guid LinkedWorkItemSpaceId)
{
    public static WorkItemLinkDto FromView(WorkItemLinkView link) => new(
        link.Id,
        link.Type,
        link.LinkedWorkItemId,
        link.LinkedWorkItemNumber,
        link.LinkedWorkItemTitle,
        link.LinkedWorkItemSpaceId);
}
