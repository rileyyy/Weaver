using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// The work item after a write, carrying its new version, and the fields the write changed.
/// <see cref="ChangedFields"/> is empty when the write set every field to the value it
/// already had.
/// </summary>
public record WorkItemWriteResponse(WorkItemDto Item, IReadOnlyList<string> ChangedFields)
{
    public static WorkItemWriteResponse FromResult(WorkItemWriteResult result, WorkItemDto item) =>
        new(item, result.ChangedFields);
}
