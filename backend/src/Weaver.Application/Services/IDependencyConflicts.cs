namespace Weaver.Application.Services;

/// <summary>
/// Finds dependency conflicts (ADR-0009): blocked items scheduled to start before a live blocker
/// ends. Weaver only reports them; fixing the dates is a person's call.
/// </summary>
public interface IDependencyConflicts
{
    /// <summary>
    /// Every current conflict, or only those with either item in <paramref name="spaceId"/>, since
    /// a conflict across Spaces may be fixed on either side.
    /// </summary>
    Task<IReadOnlyList<DependencyConflict>> ListAsync(Guid? spaceId = null, CancellationToken ct = default);
}
