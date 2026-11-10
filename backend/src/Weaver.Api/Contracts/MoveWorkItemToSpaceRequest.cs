namespace Weaver.Api.Contracts;

/// <summary>
/// Moves a work item and its subtree to <see cref="TargetSpaceId"/>, under <see cref="ParentId"/>
/// there or at its top level. Each map goes from a value of the item's Space to one of the
/// target's (null clears a Type or priority); values left out take their defaults.
/// </summary>
public record MoveWorkItemToSpaceRequest(
    Guid TargetSpaceId,
    Guid? ParentId = null,
    IReadOnlyDictionary<Guid, Guid>? StatusMap = null,
    IReadOnlyDictionary<Guid, Guid?>? TypeMap = null,
    IReadOnlyDictionary<Guid, Guid?>? PriorityMap = null,
    uint? ExpectedVersion = null);
