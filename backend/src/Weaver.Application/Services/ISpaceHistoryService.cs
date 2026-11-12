namespace Weaver.Application.Services;

/// <summary>
/// Reads a Space's history. Like work item history it is append-only, so nothing here edits or
/// deletes an entry: <see cref="ISpaceService"/> appends them.
/// </summary>
/// <remarks>
/// Kept out of the work item changes feed on purpose: a Space changes a handful of times in its
/// life, and agents asking what changed want work items, not admin configuration.
/// </remarks>
public interface ISpaceHistoryService
{
    /// <summary>
    /// Every entry for the Space, newest first, unpaged since there are only ever a few.
    /// Throws <see cref="Weaver.Domain.Exceptions.EntityNotFoundException"/> for an unknown Space.
    /// </summary>
    Task<IReadOnlyList<SpaceHistoryView>> ListForSpaceAsync(Guid spaceId, CancellationToken ct = default);
}
