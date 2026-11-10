namespace Weaver.Application.Services;

/// <summary>
/// Where one status, Type or priority of the moving items goes in the target Space by default,
/// or null for a Type or priority that would be cleared. <see cref="ItemCount"/> is how many of
/// the moving items use it.
/// </summary>
public sealed record SpaceMoveMapping(Guid SourceId, string SourceName, Guid? TargetId, string? TargetName, int ItemCount);
