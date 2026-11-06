namespace Weaver.Domain;

/// <summary>
/// The status, Type and priority a work item takes in the Space it's moving to: all of them
/// the target Space's own, since configuration lives on the Space (ADR-0001).
/// </summary>
public sealed record SpaceMoveValues(Guid StatusId, Guid? TypeId, Guid? PriorityId);
