using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// One page of work items in brief. Pass <see cref="NextCursor"/> back, with the same query, for
/// the next page; it's null on the last one.
/// </summary>
public record WorkItemSummaryPageDto(IReadOnlyList<WorkItemSummaryDto> Items, string? NextCursor)
{
    public static WorkItemSummaryPageDto FromPage(WorkItemQueryPage<WorkItemSummary> page) => new(
        page.Items.Select(WorkItemSummaryDto.FromSummary).ToList(),
        page.NextCursor);
}
