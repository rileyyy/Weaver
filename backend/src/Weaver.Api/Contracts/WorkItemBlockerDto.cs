using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// A live item blocking a <see cref="WorkItemDto"/>, possibly in another Space, with enough to
/// name it on a badge and draw its Roadmap arrow. <see cref="Conflicts"/> is true when the blocked
/// item starts before this blocker ends (a dependency conflict, ADR-0009); a Done blocker is listed
/// but never conflicts.
/// </summary>
public record WorkItemBlockerDto(Guid Id, int Number, string Title, bool Conflicts)
{
    public static WorkItemBlockerDto FromBlocker(WorkItemBlocker blocker) =>
        new(blocker.Id, blocker.Number, blocker.Title, blocker.Conflicts);
}
