using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A link as seen from one particular work item's perspective — [LinkedWorkItemId]/
/// [LinkedWorkItemTitle] are always "the other side," regardless of which of the
/// two items originally created the link.
/// </summary>
public record WorkItemLinkDto(Guid Id, Guid LinkedWorkItemId, string LinkedWorkItemTitle)
{
    public static WorkItemLinkDto FromEntity(WorkItemLink link, Guid perspectiveWorkItemId)
    {
        var other = link.WorkItemId == perspectiveWorkItemId ? link.LinkedWorkItem! : link.WorkItem!;
        return new WorkItemLinkDto(link.Id, other.Id, other.Title);
    }
}

public record CreateWorkItemLinkRequest(Guid TargetWorkItemId);
