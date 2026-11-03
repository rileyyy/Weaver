using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Anyone signed in, agents included, can list Spaces; creating and renaming one needs a human
/// admin. Both are recorded in the Space's history.
/// </summary>
public interface ISpaceService
{
    /// <summary>Every Space, by name.</summary>
    Task<IReadOnlyList<Space>> GetAllAsync(CancellationToken ct = default);

    Task<Space?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Rejects a name another Space has, ignoring case. The new Space starts with the default
    /// statuses, priorities and Types (<see cref="SpaceDefaults"/>).
    /// </summary>
    Task<Space> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>Rejects a name another Space has, ignoring case.</summary>
    Task<Space> RenameAsync(Guid id, string name, CancellationToken ct = default);
}
