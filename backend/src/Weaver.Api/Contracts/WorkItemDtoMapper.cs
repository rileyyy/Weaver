using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// Builds <see cref="WorkItemDto"/>s for every read and write that returns items, looking up
/// blockers (for <see cref="WorkItemDto.WhenResolved"/>) once per response rather than per item,
/// and only for items whose When has no dates.
/// </summary>
public class WorkItemDtoMapper(IWorkItemBlockers blockers)
{
    private readonly IWorkItemBlockers _blockers = blockers;

    public async Task<WorkItemDto> ToDtoAsync(WorkItem item, CancellationToken ct = default) =>
        (await MapperForAsync([item], ct))(item);

    public async Task<IReadOnlyList<WorkItemDto>> ToDtosAsync(IReadOnlyCollection<WorkItem> items, CancellationToken ct = default)
    {
        var toDto = await MapperForAsync(items, ct);
        return items.Select(toDto).ToList();
    }

    public async Task<IReadOnlyList<SwimlaneDto>> ToSwimlaneDtosAsync(IReadOnlyCollection<Swimlane> swimlanes, CancellationToken ct = default)
    {
        var toDto = await MapperForAsync(swimlanes.SelectMany(s => s.Cards.Prepend(s.Lane)), ct);
        return swimlanes.Select(s => SwimlaneDto.FromSwimlane(s, toDto)).ToList();
    }

    public async Task<WorkItemPageDto> ToPageDtoAsync(WorkItemQueryPage<WorkItem> page, CancellationToken ct = default) =>
        WorkItemPageDto.FromPage(page, await MapperForAsync(page.Items, ct));

    public async Task<WorkItemWriteResponse> ToWriteResponseAsync(WorkItemWriteResult result, CancellationToken ct = default) =>
        WorkItemWriteResponse.FromResult(result, await ToDtoAsync(result.Item, ct));

    private async Task<Func<WorkItem, WorkItemDto>> MapperForAsync(IEnumerable<WorkItem> items, CancellationToken ct)
    {
        var candidates = items.Where(i => i.WhenResolutionNeedsBlockers).Select(i => i.Id).Distinct().ToList();
        IReadOnlySet<Guid> blocked = candidates.Count == 0
            ? new HashSet<Guid>()
            : await _blockers.BlockedAmongAsync(candidates, ct);
        return item => WorkItemDto.FromEntity(item, blocked.Contains(item.Id));
    }
}
