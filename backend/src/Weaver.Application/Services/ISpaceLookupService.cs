using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>A lookup each Space owns its own set of, such as statuses and priorities (ADR-0001).</summary>
public interface ISpaceLookupService<T> : ILookupService<T> where T : SpaceLookupValue
{
    /// <summary>The Space's values in order; empty for an unknown Space.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(Guid spaceId, CancellationToken ct = default);

    /// <summary><paramref name="orderedIds"/> must list every value of the Space exactly once.</summary>
    Task<IReadOnlyList<T>> ReorderAsync(
        Guid actorUserId,
        Guid spaceId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default);
}
