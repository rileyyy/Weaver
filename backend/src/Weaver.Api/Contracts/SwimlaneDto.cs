using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record SwimlaneDto(WorkItemDto Lane, IReadOnlyList<WorkItemDto> Cards)
{
    public static SwimlaneDto FromSwimlane(Swimlane swimlane, Func<WorkItem, WorkItemDto> toDto) => new(
        toDto(swimlane.Lane),
        swimlane.Cards.Select(toDto).ToList());
}
