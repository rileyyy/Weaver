using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// What every lookup (statuses, types, priorities) supports. Listing, creating, editing and
/// reordering differ by kind and by whether a Space owns the values, so those are on the
/// narrower interfaces. Every change needs an admin.
/// </summary>
public interface ILookupService<T> where T : LookupValue
{
    /// <summary>
    /// Deletes the value and moves the work items using it to <paramref name="replacementId"/>,
    /// which must be one of the same values (for a Space's lookup, in the same Space). Whether
    /// a missing replacement is allowed depends on the kind: items can go without a type or
    /// priority, but not without a status.
    /// </summary>
    Task DeleteAsync(Guid actorUserId, Guid id, Guid? replacementId, CancellationToken ct = default);
}
