using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IStatusService : ISpaceLookupService<Status>
{
    /// <summary>Appends a status after the last of <paramref name="spaceId"/>'s columns.</summary>
    Task<Status> CreateAsync(
        Guid actorUserId,
        Guid spaceId,
        string name,
        StatusCategory category,
        string color,
        CancellationToken ct = default);

    Task<Status> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        StatusCategory category,
        string color,
        CancellationToken ct = default);
}
