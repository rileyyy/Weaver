using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A link as seen from one particular work item's perspective — [LinkedWorkItemId]/
/// [LinkedWorkItemTitle] are always "the other side," regardless of which of the
/// two items originally created the link.
/// </summary>
public record WorkItemLinkDto(Guid Id, Guid LinkedWorkItemId, string LinkedWorkItemTitle)
{
    public static WorkItemLinkDto FromView(WorkItemLinkView link) =>
        new(link.Id, link.LinkedWorkItemId, link.LinkedWorkItemTitle);
}
