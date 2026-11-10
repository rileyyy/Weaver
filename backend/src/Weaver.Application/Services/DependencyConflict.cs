namespace Weaver.Application.Services;

/// <summary>A blocked item that starts before its blocker ends; see <see cref="IDependencyConflicts"/>.</summary>
public sealed record DependencyConflict(DependencyConflictItem Blocked, DependencyConflictItem Blocker);
