using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// A lookup each Space owns its own set of (ADR-0001): names, order, the minimum count and
/// replacements all apply within one Space, never across.
/// </summary>
public abstract class SpaceLookupService<T>(IWeaverDbContext db, IAdminAuthorizer admins, ICallerContext caller)
    : LookupService<T>(db, admins, caller), ISpaceLookupService<T> where T : SpaceLookupValue
{
    public Task<IReadOnlyList<T>> GetAllAsync(Guid spaceId, CancellationToken ct = default) =>
        ListAsync(InSpace(spaceId), ct);

    public Task<IReadOnlyList<T>> ReorderAsync(
        Guid actorUserId,
        Guid spaceId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default) =>
        ReorderWithinAsync(actorUserId, InSpace(spaceId), orderedIds, ct);

    protected override IQueryable<T> SiblingsOf(T value) => InSpace(value.SpaceId);

    protected async Task<T> AddAsync(Guid actorUserId, Guid spaceId, Func<int, T> create, CancellationToken ct)
    {
        if (!await Db.Spaces.AnyAsync(s => s.Id == spaceId, ct))
        {
            throw new EntityNotFoundException(nameof(Space), spaceId);
        }

        return await AddWithinAsync(actorUserId, InSpace(spaceId), create, ct);
    }

    private IQueryable<T> InSpace(Guid spaceId) => Values.Where(v => v.SpaceId == spaceId);
}
