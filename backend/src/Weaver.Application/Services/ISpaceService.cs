using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Anyone signed in, agents included, can list Spaces; creating, renaming and changing a Space's
/// settings need a human admin. Each change is recorded in the Space's history.
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

    /// <summary>
    /// How many days the Board keeps showing a Done item after its completion date, 1 to 3650
    /// (<see cref="Space.DoneHidingDays"/>).
    /// </summary>
    Task<Space> SetDoneHidingPeriodAsync(Guid id, int days, CancellationToken ct = default);
}
