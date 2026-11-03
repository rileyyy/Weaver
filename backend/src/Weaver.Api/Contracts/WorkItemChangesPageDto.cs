using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// One page of changed work items. Pass <see cref="NextCursor"/> back, with the same query, for
/// the next page; it's null on the last one.
/// </summary>
public record WorkItemChangesPageDto(IReadOnlyList<ChangedWorkItemDto> Items, string? NextCursor)
{
    public static WorkItemChangesPageDto FromPage(WorkItemChangesPage page) => new(
        page.Items.Select(ChangedWorkItemDto.FromChange).ToList(),
        page.NextCursor);
}
