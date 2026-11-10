using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

internal static class SpaceExistence
{
    public static async Task EnsureSpaceExistsAsync(this IWeaverDbContext db, Guid spaceId, CancellationToken ct)
    {
        if (!await db.Spaces.AnyAsync(s => s.Id == spaceId, ct))
        {
            throw new EntityNotFoundException(nameof(Space), spaceId);
        }
    }
}
