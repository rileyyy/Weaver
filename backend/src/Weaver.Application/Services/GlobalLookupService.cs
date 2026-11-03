using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>A lookup whose values are shared by every Space, so every value is a sibling.</summary>
public abstract class GlobalLookupService<T>(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller)
    : LookupService<T>(db, admins, caller), IGlobalLookupService<T> where T : LookupValue
{
    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) => ListAsync(Values, ct);

    public Task<IReadOnlyList<T>> ReorderAsync(
        Guid actorUserId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default) =>
        ReorderWithinAsync(actorUserId, Values, orderedIds, ct);

    protected override IQueryable<T> SiblingsOf(T value) => Values;

    protected Task<T> AddAsync(Guid actorUserId, Func<int, T> create, CancellationToken ct) =>
        AddWithinAsync(actorUserId, Values, create, ct);
}
