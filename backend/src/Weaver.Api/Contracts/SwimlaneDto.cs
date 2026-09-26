using Weaver.Infrastructure.Services;

namespace Weaver.Api.Contracts;

public record SwimlaneDto(WorkItemDto Lane, IReadOnlyList<WorkItemDto> Cards)
{
    public static SwimlaneDto FromSwimlane(Swimlane swimlane) => new(
        WorkItemDto.FromEntity(swimlane.Lane),
        swimlane.Cards.Select(WorkItemDto.FromEntity).ToList());
}
