namespace Weaver.Domain;

/// <summary>
/// The status, Type and priority a work item takes in the Space it's moving to: all of them
/// the target Space's own, since configuration lives on the Space (ADR-0001). The status is the
/// whole entity because its category decides the item's completion date.
/// </summary>
public sealed record SpaceMoveValues(Status Status, Guid? TypeId, Guid? PriorityId);
