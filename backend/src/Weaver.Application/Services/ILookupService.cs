using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// What every lookup (statuses, layers, priorities) supports. Creating and editing differ by
/// kind, so those are on each kind's own interface. Every change needs an admin.
/// </summary>
public interface ILookupService<T> where T : LookupValue
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary><paramref name="orderedIds"/> must list every value exactly once.</summary>
    Task<IReadOnlyList<T>> ReorderAsync(Guid actorUserId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);

    /// <summary>
    /// Deletes the value and moves the work items using it to <paramref name="replacementId"/>.
    /// Whether a missing replacement is allowed depends on the kind: items can go without a
    /// layer or priority, but not without a status.
    /// </summary>
    Task DeleteAsync(Guid actorUserId, Guid id, Guid? replacementId, CancellationToken ct = default);
}
