using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>A lookup shared by every Space, such as types.</summary>
public interface IGlobalLookupService<T> : ILookupService<T> where T : LookupValue
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary><paramref name="orderedIds"/> must list every value exactly once.</summary>
    Task<IReadOnlyList<T>> ReorderAsync(Guid actorUserId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
