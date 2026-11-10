namespace Weaver.Application.Services;

/// <summary>
/// A live item blocking another, named so a reader needs no lookups. <see cref="Conflicts"/> says
/// whether the blocked item is a dependency conflict because of it (see <see cref="IDependencyConflicts"/>).
/// </summary>
public sealed record WorkItemBlocker(Guid Id, int Number, string Title, bool Conflicts);
