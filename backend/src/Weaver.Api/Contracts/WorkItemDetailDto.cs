namespace Weaver.Api.Contracts;

/// <summary>
/// An item with its roll-ups, for MCP's <c>get_work_item</c>: the same JSON as a
/// <see cref="WorkItemDto"/> plus <see cref="RollUps"/>. Only this full-detail read carries them,
/// since summing descendants for every item of a listing would walk each one's subtree. REST
/// readers get them from <c>GET /api/work-items/{id}/roll-ups</c>.
/// </summary>
public sealed record WorkItemDetailDto : WorkItemDto
{
    public WorkItemDetailDto(WorkItemDto item, IReadOnlyList<RollUpDto> rollUps)
        : base(item)
    {
        RollUps = rollUps;
    }

    public IReadOnlyList<RollUpDto> RollUps { get; }
}
