using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// One page of work items. Pass <see cref="NextCursor"/> back, with the same query, for the next
/// page; it's null on the last one.
/// </summary>
public record WorkItemPageDto(IReadOnlyList<WorkItemDto> Items, string? NextCursor)
{
    public static WorkItemPageDto FromPage(WorkItemQueryPage<WorkItem> page) => new(
        page.Items.Select(WorkItemDto.FromEntity).ToList(),
        page.NextCursor);
}
