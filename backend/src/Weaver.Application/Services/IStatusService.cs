using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IStatusService : ILookupService<Status>
{
    Task<Status> CreateAsync(
        Guid actorUserId,
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
